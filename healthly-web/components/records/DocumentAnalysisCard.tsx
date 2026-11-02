'use client';

// ИИ-анализ документа на странице документа: кнопка «Analyze with AI» → таблица показателей с оценкой по справочнику норм.
//   - критические значения — красная строка и красный баннер над таблицей (срочно к врачу);
//   - значения, похожие на ошибку распознавания (confidence ≤ 0.4), — жёлтая пометка «Check against the report»;
//   - резюме и советы ИИ, к каким врачам обратиться, дисклеймер.
// Результат пока не сохраняется (см. PROJECT_GUIDE.md, Roadmap: черновик анализа и подтверждение).

import { useState } from 'react';
import { AlertTriangle, Sparkles } from 'lucide-react';
import Card from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import Badge from '@/components/ui/Badge';
import { documentsApi, getErrorMessage } from '@/lib/api';
import { UNCERTAIN_CONFIDENCE, type AnalyzedIndicator, type DocumentAnalysis } from '@/types/api';

function needsCheck(indicator: AnalyzedIndicator) {
  return indicator.confidence <= UNCERTAIN_CONFIDENCE;
}

function IndicatorStatus({ indicator }: { indicator: AnalyzedIndicator }) {
  if (indicator.isCritical) {
    return <Badge tone="red">Critical</Badge>;
  }
  if (indicator.isAbnormal === null) {
    return needsCheck(indicator) ? <Badge tone="amber">Check value</Badge> : <Badge>Unknown</Badge>;
  }
  return indicator.isAbnormal ? <Badge tone="amber">Out of range</Badge> : <Badge tone="green">Normal</Badge>;
}

function CriticalBanner({ indicators }: { indicators: AnalyzedIndicator[] }) {
  const critical = indicators.filter((indicator) => indicator.isCritical);
  if (critical.length === 0) {
    return null;
  }
  // Критическое значение, похожее на ошибку распознавания, всё равно показываем — но просим сначала сверить с бланком
  const allUncertain = critical.every(needsCheck);

  return (
    <div role="alert" className="flex items-start gap-3 rounded-2xl border-2 border-red-500 bg-red-50 px-4 py-3 text-red-800">
      <AlertTriangle className="w-5 h-5 mt-0.5 shrink-0" />
      <div className="text-sm">
        <p className="font-semibold">
          {allUncertain ? 'Possible critical value' : 'Critical value'}
          {critical.length > 1 ? 's' : ''}:{' '}
          {critical.map((indicator) => `${indicator.testName} ${indicator.value} ${indicator.unit}`.trim()).join(', ')}
        </p>
        <p className="mt-1">
          {allUncertain
            ? 'This value may also be a reading error. Check it against the original report and, if it is confirmed, contact your doctor right away.'
            : 'Contact your doctor right away or seek urgent medical care.'}
        </p>
      </div>
    </div>
  );
}

export default function DocumentAnalysisCard({ documentId }: { documentId: number }) {
  const [analysis, setAnalysis] = useState<DocumentAnalysis | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isAnalyzing, setIsAnalyzing] = useState(false);

  const handleAnalyze = async () => {
    setError(null);
    setIsAnalyzing(true);
    try {
      setAnalysis(await documentsApi.analyze(documentId));
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsAnalyzing(false);
    }
  };

  return (
    <Card
      title="AI analysis"
      actions={
        <Button size="sm" onClick={handleAnalyze} isLoading={isAnalyzing}>
          <Sparkles className="w-3.5 h-3.5" />
          {analysis ? 'Analyze again' : 'Analyze with AI'}
        </Button>
      }
    >
      {isAnalyzing && (
        <p className="text-sm text-[#6b756e]">Reading the report and analyzing the values. This can take up to a minute…</p>
      )}
      {error && <Alert>{error}</Alert>}
      {!analysis && !isAnalyzing && !error && (
        <p className="text-sm text-[#6b756e]">
          Recognize the values in this report, compare them with the reference norms and get a plain-language summary.
        </p>
      )}

      {analysis && !isAnalyzing && (
        <div className="space-y-5">
          <CriticalBanner indicators={analysis.indicators} />

          {analysis.indicators.some(needsCheck) && (
            <p className="text-sm rounded-2xl bg-amber-50 text-amber-800 px-4 py-3">
              Values marked <span className="font-semibold">Check value</span> may have been read incorrectly. Compare them
              with the original report before relying on them.
            </p>
          )}

          <div className="overflow-x-auto -mx-5 sm:-mx-6">
            <table className="w-full min-w-[560px] text-sm">
              <thead>
                <tr className="text-left text-xs text-[#6b756e] border-b border-[#e1e7dc]">
                  <th className="font-medium px-5 sm:px-6 py-2">Test</th>
                  <th className="font-medium px-3 py-2">Value</th>
                  <th className="font-medium px-3 py-2">Reference range</th>
                  <th className="font-medium px-5 sm:px-6 py-2">Status</th>
                </tr>
              </thead>
              <tbody>
                {analysis.indicators.map((indicator, index) => (
                  <tr
                    key={`${indicator.testName}-${index}`}
                    className={`border-b border-[#e1e7dc] last:border-0 ${
                      indicator.isCritical ? 'bg-red-50 text-red-800' : needsCheck(indicator) ? 'bg-amber-50/60' : ''
                    }`}
                  >
                    <td className="px-5 sm:px-6 py-3 font-medium">
                      <span className="inline-flex items-center gap-1.5">
                        {indicator.isCritical && <AlertTriangle className="w-4 h-4 text-red-600" aria-label="Critical" />}
                        {indicator.testName}
                      </span>
                    </td>
                    <td className="px-3 py-3">
                      {indicator.value} <span className="text-[#6b756e]">{indicator.unit}</span>
                    </td>
                    <td className="px-3 py-3 text-[#6b756e]">{indicator.referenceRange || '—'}</td>
                    <td className="px-5 sm:px-6 py-3"><IndicatorStatus indicator={indicator} /></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {analysis.summary && (
            <div>
              <h3 className="text-sm font-semibold mb-1">Summary</h3>
              <p className="text-sm text-[#242b26]">{analysis.summary}</p>
            </div>
          )}

          {analysis.recommendations.length > 0 && (
            <div>
              <h3 className="text-sm font-semibold mb-1">Recommendations</h3>
              <ul className="list-disc pl-5 space-y-1 text-sm">
                {analysis.recommendations.map((recommendation) => (
                  <li key={recommendation}>{recommendation}</li>
                ))}
              </ul>
            </div>
          )}

          {analysis.suggestedSpecialists.length > 0 && (
            <div className="flex flex-wrap items-center gap-2 text-sm">
              <span className="font-semibold">Suggested specialists:</span>
              {analysis.suggestedSpecialists.map((specialist) => (
                <Badge key={specialist} tone="blue">{specialist}</Badge>
              ))}
            </div>
          )}

          <p className="text-xs text-[#6b756e] border-t border-[#e1e7dc] pt-3">
            {analysis.disclaimer}
            {analysis.isMock && ' Demo mode: the values above are generated, not read from the file.'}
          </p>
        </div>
      )}
    </Card>
  );
}
