'use client';

// ИИ-анализ на странице документа — «ИИ предлагает, человек подтверждает»:
//   1. «Analyze with AI» → черновик сохраняется на сервере (повторное открытие страницы — без нового вызова ИИ);
//   2. человек сверяет таблицу с бланком: правит значение/единицу/название, удаляет лишние строки, отмечает
//      сомнительные значения («Check value», confidence ≤ 0.4) как проверенные — без этого подтвердить нельзя;
//   3. «Confirm & save» → показатели заменяют таблицу «Test results» документа и попадают в историю и графики.
// Критические значения — красная строка и красный баннер. «В норме / нет» решает сервер по справочнику норм.

import { useCallback, useState } from 'react';
import { CheckCircle2, RotateCcw, Sparkles, Trash2 } from 'lucide-react';
import Card, { LoadingState } from '@/components/ui/Card';
import Button from '@/components/ui/Button';
import Alert from '@/components/ui/Alert';
import Badge, { CriticalBadge } from '@/components/ui/Badge';
import ConfirmDialog from '@/components/ui/ConfirmDialog';
import CriticalBanner from './CriticalBanner';
import { documentsApi, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/format';
import { useApiData } from '@/lib/useApiData';
import {
  UNCERTAIN_CONFIDENCE,
  type AnalyzedIndicator,
  type DocumentAnalysis,
  type MedicalDocumentDetails,
} from '@/types/api';

interface DocumentAnalysisCardProps {
  documentId: number;
  existingResultsCount: number;
  onConfirmed: (document: MedicalDocumentDetails) => void;
}

function needsCheck(indicator: AnalyzedIndicator) {
  return indicator.confidence <= UNCERTAIN_CONFIDENCE;
}

function IndicatorStatus({ indicator }: { indicator: AnalyzedIndicator }) {
  if (indicator.isCritical) {
    return <CriticalBadge />;
  }
  if (indicator.isAbnormal === null) {
    return needsCheck(indicator) ? <Badge tone="amber">Check value</Badge> : <Badge>Unknown</Badge>;
  }
  return indicator.isAbnormal ? <Badge tone="red">Out of range</Badge> : <Badge tone="green">Normal</Badge>;
}

function rowClass(indicator: AnalyzedIndicator) {
  if (indicator.isCritical) {
    return 'bg-red-50 text-red-800';
  }
  return needsCheck(indicator) ? 'bg-amber-50/60' : '';
}

export default function DocumentAnalysisCard({ documentId, existingResultsCount, onConfirmed }: DocumentAnalysisCardProps) {
  // { analysis: null } — документ ещё не анализировали; data === null — идёт загрузка
  const load = useCallback(() => documentsApi.getAnalysis(documentId).then((analysis) => ({ analysis })), [documentId]);
  const { data, error: loadError, setData } = useApiData(load);
  const [error, setError] = useState<string | null>(null);
  const [isAnalyzing, setIsAnalyzing] = useState(false);
  const [isReanalyzeOpen, setIsReanalyzeOpen] = useState(false);

  const runAnalysis = async (force: boolean) => {
    setError(null);
    setIsAnalyzing(true);
    try {
      setData({ analysis: await documentsApi.analyze(documentId, force) });
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsAnalyzing(false);
    }
  };

  const analysis = data?.analysis ?? null;

  return (
    <Card
      title="AI analysis"
      actions={
        analysis ? (
          <Button variant="secondary" size="sm" onClick={() => setIsReanalyzeOpen(true)} disabled={isAnalyzing}>
            <RotateCcw className="w-3.5 h-3.5" />
            Analyze again
          </Button>
        ) : (
          data && (
            <Button size="sm" onClick={() => runAnalysis(false)} isLoading={isAnalyzing}>
              <Sparkles className="w-3.5 h-3.5" />
              Analyze with AI
            </Button>
          )
        )
      }
    >
      {loadError && <Alert>{loadError}</Alert>}
      {!data && !loadError && <LoadingState label="Loading analysis…" />}
      {error && <Alert className="mb-4">{error}</Alert>}

      {isAnalyzing && (
        <p className="text-sm text-[#6b756e]">Reading the report and analyzing the values. This can take up to a minute…</p>
      )}

      {data && !analysis && !isAnalyzing && (
        <p className="text-sm text-[#6b756e]">
          Recognize the values in this report, compare them with the reference norms and get a plain-language summary.
          You will check the values against the report before they are saved.
        </p>
      )}

      {analysis && !isAnalyzing && (
        // key: новый анализ — новая форма (правки старого черновика не переносятся)
        <AnalysisView
          key={`${analysis.analyzedAt}-${analysis.confirmedAt ?? 'draft'}`}
          documentId={documentId}
          analysis={analysis}
          existingResultsCount={existingResultsCount}
          onConfirmed={(document) => {
            setData({ analysis: { ...analysis, confirmedAt: new Date().toISOString() } });
            onConfirmed(document);
          }}
        />
      )}

      <ConfirmDialog
        isOpen={isReanalyzeOpen}
        title="Analyze again"
        message="The report will be read by the AI again (this uses the AI quota) and the current analysis will be replaced. Saved test results stay until you confirm the new analysis."
        confirmLabel="Analyze again"
        onClose={() => setIsReanalyzeOpen(false)}
        onConfirm={() => runAnalysis(true)}
      />
    </Card>
  );
}

interface EditableRow {
  indicator: AnalyzedIndicator;
  testName: string;
  value: string;
  unit: string;
  checked: boolean;
  removed: boolean;
}

function isEdited(row: EditableRow) {
  return row.testName !== row.indicator.testName || row.value !== row.indicator.value || row.unit !== row.indicator.unit;
}

function AnalysisView({
  documentId,
  analysis,
  existingResultsCount,
  onConfirmed,
}: {
  documentId: number;
  analysis: DocumentAnalysis;
  existingResultsCount: number;
  onConfirmed: (document: MedicalDocumentDetails) => void;
}) {
  const isConfirmed = analysis.confirmedAt !== null;
  const [rows, setRows] = useState<EditableRow[]>(() =>
    analysis.indicators.map((indicator) => ({
      indicator,
      testName: indicator.testName,
      value: indicator.value,
      unit: indicator.unit,
      checked: false,
      removed: false,
    })),
  );
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);
  const [isReplaceOpen, setIsReplaceOpen] = useState(false);

  const updateRow = (index: number, change: Partial<EditableRow>) =>
    setRows((current) => current.map((row, i) => (i === index ? { ...row, ...change } : row)));

  const activeRows = rows.filter((row) => !row.removed);
  // Сомнительное значение нужно проверить: отметить или исправить
  const uncheckedCount = activeRows.filter((row) => needsCheck(row.indicator) && !row.checked && !isEdited(row)).length;
  const hasEmptyValues = activeRows.some((row) => !row.testName.trim() || !row.value.trim());
  const canConfirm = activeRows.length > 0 && uncheckedCount === 0 && !hasEmptyValues;

  const save = async () => {
    setError(null);
    setIsSaving(true);
    try {
      const document = await documentsApi.confirmAnalysis(documentId, {
        indicators: activeRows.map((row) => ({
          testName: row.testName.trim(),
          value: row.value.trim(),
          unit: row.unit.trim() || null,
          referenceRange: row.indicator.referenceRange || null,
        })),
      });
      onConfirmed(document);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setIsSaving(false);
    }
  };

  const criticalItems = analysis.indicators
    .filter((indicator) => indicator.isCritical)
    .map((indicator) => ({ ...indicator, uncertain: needsCheck(indicator) }));

  return (
    <div className="space-y-5">
      {/* Подтверждённые значения (с правками человека) — в таблице «Test results» ниже, там же и красная подсветка */}
      {isConfirmed ? (
        <Alert variant="success">
          Confirmed on {formatDate(analysis.confirmedAt!)}. The checked values are saved in Test results below and in the health history.
        </Alert>
      ) : (
        <p className="text-sm rounded-2xl bg-[#f3f6ef] text-[#3a5235] px-4 py-3">
          <span className="font-semibold">Draft.</span> Compare each value with the original report, fix anything that was read
          incorrectly and remove rows that are not test results. Then confirm to save the values.
          {analysis.indicators.some(needsCheck) && (
            <> Values marked <span className="font-semibold">Check value</span> must be checked first.</>
          )}
        </p>
      )}

      {analysis.patientDataNote && (
        <p className="text-sm rounded-2xl bg-amber-50 text-amber-800 px-4 py-3">{analysis.patientDataNote}</p>
      )}

      {!isConfirmed && <CriticalBanner items={criticalItems} />}

      {!isConfirmed && (
      <div className="overflow-x-auto -mx-5 sm:-mx-6">
        <table className="w-full min-w-[640px] text-sm">
          <thead>
            <tr className="text-left text-xs text-[#6b756e] border-b border-[#e1e7dc]">
              <th className="font-medium px-5 sm:px-6 py-2">Test</th>
              <th className="font-medium px-3 py-2">Value</th>
              <th className="font-medium px-3 py-2">Unit</th>
              <th className="font-medium px-3 py-2">Reference range</th>
              <th className="font-medium px-3 py-2">Status</th>
              {!isConfirmed && <th className="font-medium px-5 sm:px-6 py-2 text-right">Check</th>}
            </tr>
          </thead>
          <tbody>
            {rows.map((row, index) =>
              row.removed ? null : (
                <tr key={index} className={`border-b border-[#e1e7dc] last:border-0 ${rowClass(row.indicator)}`}>
                  {isConfirmed ? (
                    <>
                      <td className="px-5 sm:px-6 py-3 font-medium">{row.testName}</td>
                      <td className="px-3 py-3">{row.value}</td>
                      <td className="px-3 py-3 text-[#6b756e]">{row.unit}</td>
                    </>
                  ) : (
                    <>
                      <td className="px-5 sm:px-6 py-2">
                        <CellInput label="Test name" value={row.testName} onChange={(testName) => updateRow(index, { testName })} />
                      </td>
                      <td className="px-3 py-2">
                        <CellInput label="Value" value={row.value} narrow onChange={(value) => updateRow(index, { value })} />
                      </td>
                      <td className="px-3 py-2">
                        <CellInput label="Unit" value={row.unit} narrow onChange={(unit) => updateRow(index, { unit })} />
                      </td>
                    </>
                  )}
                  <td className="px-3 py-3 text-[#6b756e]">{row.indicator.referenceRange || '—'}</td>
                  <td className="px-3 py-3">
                    {isEdited(row) ? <Badge>Edited</Badge> : <IndicatorStatus indicator={row.indicator} />}
                  </td>
                  {!isConfirmed && (
                    <td className="px-5 sm:px-6 py-2">
                      <div className="flex items-center justify-end gap-2">
                        {needsCheck(row.indicator) && !isEdited(row) && (
                          <label className="inline-flex items-center gap-1.5 text-xs whitespace-nowrap">
                            <input
                              type="checkbox"
                              checked={row.checked}
                              onChange={(event) => updateRow(index, { checked: event.target.checked })}
                            />
                            Checked
                          </label>
                        )}
                        <Button variant="ghost" size="sm" aria-label="Remove row" onClick={() => updateRow(index, { removed: true })}>
                          <Trash2 className="w-4 h-4" />
                        </Button>
                      </div>
                    </td>
                  )}
                </tr>
              ),
            )}
          </tbody>
        </table>
      </div>
      )}

      {!isConfirmed && (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs text-[#6b756e]">
            {uncheckedCount > 0
              ? `${uncheckedCount} value${uncheckedCount > 1 ? 's' : ''} still need${uncheckedCount > 1 ? '' : 's'} checking.`
              : 'Edited values are evaluated against the norms again when you save.'}
          </p>
          <Button
            size="sm"
            disabled={!canConfirm}
            isLoading={isSaving}
            onClick={() => (existingResultsCount > 0 ? setIsReplaceOpen(true) : save())}
          >
            <CheckCircle2 className="w-3.5 h-3.5" />
            Confirm &amp; save
          </Button>
        </div>
      )}
      {error && <Alert>{error}</Alert>}

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

      <ConfirmDialog
        isOpen={isReplaceOpen}
        title="Replace test results"
        message={`This document already has ${existingResultsCount} test result${existingResultsCount > 1 ? 's' : ''}. They will be replaced by the ${activeRows.length} confirmed value${activeRows.length > 1 ? 's' : ''}.`}
        confirmLabel="Replace"
        onClose={() => setIsReplaceOpen(false)}
        onConfirm={save}
      />
    </div>
  );
}

function CellInput({ label, value, narrow = false, onChange }: { label: string; value: string; narrow?: boolean; onChange: (value: string) => void }) {
  return (
    <input
      aria-label={label}
      value={value}
      onChange={(event) => onChange(event.target.value)}
      className={`${narrow ? 'w-24' : 'w-full min-w-[9rem]'} rounded-lg border border-[#e1e7dc] bg-white px-2 py-1 text-sm outline-none focus:border-[#72826c]`}
    />
  );
}
