using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services.Strategies;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 회차별(Session/OMR 분석 결과) 캐시를 제공하는 서비스입니다.
    /// - session.json 파싱/분석 결과를 공유하여 Grading/검산 간 중복 비용을 줄입니다.
    /// - 저장 포맷은 변경하지 않습니다.
    /// </summary>
    public sealed class OmrAnalysisCache
    {
        private static readonly Lazy<OmrAnalysisCache> _instance = new(() => new OmrAnalysisCache());
        public static OmrAnalysisCache Instance => _instance.Value;

        private readonly SemaphoreSlim _gate = new(1, 1);

        private string _roundKey = "";

        private Session? _session;
        private ScoringRule? _scoringRule;
        private StudentRegistry? _studentRegistry;

        private Dictionary<string, ImageDocument>? _documentByImageId;

        private Dictionary<string, string?>? _studentIdByImageId; // barcode-only quick index

        private List<OmrSheetResult>? _allSheetResults;
        private Dictionary<string, List<OmrSheetResult>>? _sheetResultsByStudentId;

        private readonly SessionStore _sessionStore = new();
        private readonly ScoringRuleStore _scoringRuleStore = new();
        private readonly RegistryStore _registryStore = new();
        private readonly MarkingAnalyzer _markingAnalyzer = new();
        private readonly IBarcodeProcessingStrategy _barcodeStrategy = new DefaultBarcodeProcessingStrategy();

        private OmrAnalysisCache()
        {
        }

        private static string GetCurrentRoundKey() => PathService.CurrentRound ?? "";

        private void InvalidateIfRoundChanged_NoLock()
        {
            var current = GetCurrentRoundKey();
            if (!string.Equals(_roundKey, current, StringComparison.Ordinal))
            {
                _roundKey = current;
                _session = null;
                _scoringRule = null;
                _studentRegistry = null;
                _documentByImageId = null;
                _studentIdByImageId = null;
                _allSheetResults = null;
                _sheetResultsByStudentId = null;
            }
        }

        public async Task InvalidateAsync()
        {
            await _gate.WaitAsync();
            try
            {
                _roundKey = GetCurrentRoundKey();
                _session = null;
                _scoringRule = null;
                _studentRegistry = null;
                _documentByImageId = null;
                _studentIdByImageId = null;
                _allSheetResults = null;
                _sheetResultsByStudentId = null;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<Session> GetSessionAsync()
        {
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();

                if (_session != null) return _session;
                _session = await Task.Run(() => _sessionStore.Load());
                return _session;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<ScoringRule> GetScoringRuleAsync()
        {
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();

                if (_scoringRule != null) return _scoringRule;
                _scoringRule = await Task.Run(() => _scoringRuleStore.LoadScoringRule());
                return _scoringRule;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<StudentRegistry> GetStudentRegistryAsync()
        {
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();

                if (_studentRegistry != null) return _studentRegistry;
                _studentRegistry = await Task.Run(() => _registryStore.LoadStudentRegistry());
                return _studentRegistry;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<Dictionary<string, ImageDocument>> GetDocumentByImageIdAsync()
        {
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();

                if (_documentByImageId != null) return _documentByImageId;

                var session = _session ?? await Task.Run(() => _sessionStore.Load());
                _session ??= session;

                _documentByImageId = session.Documents
                    .Where(d => !string.IsNullOrEmpty(d.ImageId))
                    .GroupBy(d => d.ImageId)
                    .ToDictionary(g => g.Key, g => g.First());

                return _documentByImageId;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// barcodeResults만으로 imageId -> studentId 인덱스를 구성합니다.
        /// (marking 분석 없이 단일 학생의 시트를 찾기 위함)
        /// </summary>
        public async Task<Dictionary<string, string?>> GetStudentIdIndexByImageIdAsync()
        {
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();
                if (_studentIdByImageId != null) return _studentIdByImageId;

                var session = _session ?? await Task.Run(() => _sessionStore.Load());
                _session ??= session;

                var map = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var doc in session.Documents)
                {
                    session.BarcodeResults.TryGetValue(doc.ImageId, out var barcodeResults);

                    var tmp = new OmrSheetResult
                    {
                        ImageId = doc.ImageId,
                        ImageFileName = System.IO.Path.GetFileName(doc.SourcePath)
                    };

                    if (barcodeResults != null)
                    {
                        for (int i = 0; i < barcodeResults.Count && i < OmrConstants.BarcodeAreasCount; i++)
                        {
                            _barcodeStrategy.ApplyBarcodeResult(tmp, barcodeResults[i], i);
                        }
                    }

                    map[doc.ImageId] = string.IsNullOrWhiteSpace(tmp.StudentId) ? null : tmp.StudentId;
                }

                _studentIdByImageId = map;
                return _studentIdByImageId;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<List<OmrSheetResult>> GetAllSheetResultsAsync()
        {
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();
                if (_allSheetResults != null) return _allSheetResults;

                var session = _session ?? await Task.Run(() => _sessionStore.Load());
                _session ??= session;

                // 전체 용지 분석 (한 번만)
                var results = await Task.Run(() => _markingAnalyzer.AnalyzeAllSheets(session));

                // 결합ID 기준 중복 적용(전체 기준)
                DuplicateDetector.DetectAndApplyCombinedIdDuplicates(results);

                _allSheetResults = results;
                _sheetResultsByStudentId = results
                    .Where(r => !string.IsNullOrWhiteSpace(r.StudentId))
                    .GroupBy(r => r.StudentId!)
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

                return _allSheetResults;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// 특정 수험번호의 시트 결과만 반환합니다.
        /// - 전체 분석 결과가 이미 있으면 O(1) 수준으로 필터링
        /// - 없으면 barcode-only 인덱스로 해당 학생의 문서만 추려 subset 분석
        /// </summary>
        public async Task<List<OmrSheetResult>> GetSheetResultsForStudentAsync(string studentId)
        {
            if (string.IsNullOrWhiteSpace(studentId)) return new List<OmrSheetResult>();
            var id = studentId.Trim();

            // fast-path: full cache
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();
                if (_sheetResultsByStudentId != null && _sheetResultsByStudentId.TryGetValue(id, out var cached))
                {
                    return cached.ToList();
                }
            }
            finally
            {
                _gate.Release();
            }

            // slow-path: subset analysis (no full AnalyzeAllSheets)
            var session = await GetSessionAsync();
            var docById = await GetDocumentByImageIdAsync();
            var idx = await GetStudentIdIndexByImageIdAsync();

            var docs = session.Documents
                .Where(d => idx.TryGetValue(d.ImageId, out var sid) && string.Equals(sid, id, StringComparison.Ordinal))
                .ToList();

            if (docs.Count == 0) return new List<OmrSheetResult>();

            var subset = await Task.Run(() =>
            {
                var list = new List<OmrSheetResult>();
                foreach (var doc in docs)
                {
                    session.MarkingResults.TryGetValue(doc.ImageId, out var markingResults);
                    session.BarcodeResults.TryGetValue(doc.ImageId, out var barcodeResults);
                    list.Add(_markingAnalyzer.AnalyzeSheet(doc, markingResults, barcodeResults));
                }
                DuplicateDetector.DetectAndApplyCombinedIdDuplicates(list);
                return list;
            });

            // do not populate full caches, but optionally keep per-student cache if full dict exists
            await _gate.WaitAsync();
            try
            {
                InvalidateIfRoundChanged_NoLock();
                _sheetResultsByStudentId ??= new Dictionary<string, List<OmrSheetResult>>(StringComparer.Ordinal);
                _sheetResultsByStudentId[id] = subset;
            }
            finally
            {
                _gate.Release();
            }

            return subset;
        }
    }
}

