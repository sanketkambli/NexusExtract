import React, { useState } from 'react';
import {
  Box,
  Typography,
  Paper,
  Grid,
  TextField,
  Button,
  Chip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Select,
  MenuItem,
  Alert,
  Tooltip,
  LinearProgress,
} from '@mui/material';
import {
  Sparkles,
  CheckCircle2,
  AlertTriangle,
  Database,
  Cpu,
  Coins,
  Clock,
  Save,
  ArrowRight,
  ShieldCheck,
  RefreshCw,
} from 'lucide-react';
import { useCanonicalSchema, useAnalyzeSchema, useApproveSchema } from '../api/schemaApi';
import { FieldMappingRule, AnalyzeSchemaResponse } from '../types/api';

const PRESET_FEEDS = [
  {
    name: 'Ingram / Cisco Vendor Feed (Hardware Catalog)',
    partner: 'Cisco Systems B2B',
    columns: 'VND_PART_NO, BARCODE_EAN, DESCR, NET_COST_USD, QTY_ONHAND, DEPT_HIERARCHY, SHIP_WT_LBS',
    samples: [
      { VND_PART_NO: 'WS-C3850-24T-L', BARCODE_EAN: '012345678905', DESCR: 'Cisco Catalyst 3850 24-Port Switch', NET_COST_USD: '$450.00', QTY_ONHAND: '85', DEPT_HIERARCHY: 'Hardware > Networking', SHIP_WT_LBS: '14.5' },
      { VND_PART_NO: 'C9300-48P-A', BARCODE_EAN: '098765432109', DESCR: 'Catalyst 9300 48-Port PoE+ Switch', NET_COST_USD: '$1,250.00', QTY_ONHAND: '32', DEPT_HIERARCHY: 'Hardware > Switches', SHIP_WT_LBS: '16.2' },
    ],
  },
  {
    name: 'Medpace Clinical Trials Laboratory Inventory',
    partner: 'LabCorp Global Supplies',
    columns: 'MAT_NO, UPC_CODE, ITEM_NAME, UNIT_PRICE, UNITS_AVAILABLE, CATEGORY_CLASS',
    samples: [
      { MAT_NO: 'LAB-REAG-0091', UPC_CODE: '556677889901', ITEM_NAME: 'Blood Plasma Centrifuge Reagent 500ml', UNIT_PRICE: '$89.50', UNITS_AVAILABLE: '240', CATEGORY_CLASS: 'Diagnostics > Reagents' },
    ],
  },
  {
    name: 'Geneva Trade Blotter Holdings',
    partner: 'Geneva Portfolio System',
    columns: 'SECURITY_ID, CUSIP_NUM, ASSET_TITLE, MARKET_VALUE, QUANTITY, ASSET_CLASS',
    samples: [
      { SECURITY_ID: 'AAPL-EQ-01', CUSIP_NUM: '037833100', ASSET_TITLE: 'Apple Inc. Common Stock', MARKET_VALUE: '$225.50', QUANTITY: '5000', ASSET_CLASS: 'Equities > Tech' },
    ],
  },
];

export const SchemaMappingPage: React.FC = () => {
  const [partnerName, setPartnerName] = useState(PRESET_FEEDS[0].partner);
  const [columnsInput, setColumnsInput] = useState(PRESET_FEEDS[0].columns);
  const [activeAnalysis, setActiveAnalysis] = useState<AnalyzeSchemaResponse | null>(null);
  const [editableMappings, setEditableMappings] = useState<FieldMappingRule[]>([]);
  const [approvalSuccess, setApprovalSuccess] = useState(false);

  const { data: canonicalFields } = useCanonicalSchema();
  const analyzeMutation = useAnalyzeSchema();
  const approveMutation = useApproveSchema();

  const handleSelectPreset = (preset: typeof PRESET_FEEDS[0]) => {
    setPartnerName(preset.partner);
    setColumnsInput(preset.columns);
    setActiveAnalysis(null);
    setEditableMappings([]);
    setApprovalSuccess(false);
  };

  const handleAnalyze = () => {
    const rawCols = columnsInput
      .split(',')
      .map((c) => c.trim())
      .filter((c) => c.length > 0);

    const selectedPreset = PRESET_FEEDS.find((p) => p.partner === partnerName);
    const sampleRows = selectedPreset?.samples || [];

    setApprovalSuccess(false);

    analyzeMutation.mutate(
      {
        partnerName,
        sourceColumns: rawCols,
        sampleRows,
      },
      {
        onSuccess: (data) => {
          setActiveAnalysis(data);
          setEditableMappings(JSON.parse(JSON.stringify(data.suggestedMappings)));
        },
      }
    );
  };

  const handleMappingChange = (index: number, newTarget: string) => {
    const updated = [...editableMappings];
    updated[index].targetCanonicalField = newTarget;
    updated[index].isUserOverridden = true;
    setEditableMappings(updated);
  };

  const handleTransformChange = (index: number, newTransform: string) => {
    const updated = [...editableMappings];
    updated[index].transformationRule = newTransform;
    updated[index].isUserOverridden = true;
    setEditableMappings(updated);
  };

  const handleApprove = () => {
    if (!activeAnalysis) return;

    approveMutation.mutate(
      {
        sessionId: activeAnalysis.sessionId,
        req: {
          finalMappings: editableMappings,
          approvedBy: 'LeadEnterpriseArchitect',
        },
      },
      {
        onSuccess: () => {
          setApprovalSuccess(true);
        },
      }
    );
  };

  const getConfidenceBadge = (score: number) => {
    const pct = Math.round(score * 100);
    if (score >= 0.9) {
      return (
        <Chip
          icon={<CheckCircle2 size={13} color="#10b981" />}
          label={`${pct}% Match`}
          size="small"
          sx={{
            bgcolor: 'rgba(16, 185, 129, 0.12)',
            color: '#10b981',
            fontWeight: 700,
            fontSize: '0.73rem',
            border: '1px solid rgba(16, 185, 129, 0.3)',
          }}
        />
      );
    }
    if (score >= 0.75) {
      return (
        <Chip
          icon={<Sparkles size={13} color="#f59e0b" />}
          label={`${pct}% Match`}
          size="small"
          sx={{
            bgcolor: 'rgba(245, 158, 11, 0.12)',
            color: '#f59e0b',
            fontWeight: 700,
            fontSize: '0.73rem',
            border: '1px solid rgba(245, 158, 11, 0.3)',
          }}
        />
      );
    }
    return (
      <Chip
        icon={<AlertTriangle size={13} color="#ef4444" />}
        label={`${pct}% Review`}
        size="small"
        sx={{
          bgcolor: 'rgba(239, 68, 68, 0.12)',
          color: '#ef4444',
          fontWeight: 700,
          fontSize: '0.73rem',
          border: '1px solid rgba(239, 68, 68, 0.3)',
        }}
      />
    );
  };

  return (
    <Box>
      {/* Page Header */}
      <Box sx={{ mb: 3 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 0.5 }}>
          <Sparkles size={26} color="#38bdf8" />
          <Typography variant="h5" sx={{ fontWeight: 700, color: '#f8fafc' }}>
            AI Schema Mapping Assistant
          </Typography>
          <Chip
            label="Google Gemini 2.5 + RAG"
            size="small"
            sx={{
              background: 'linear-gradient(135deg, rgba(56, 189, 248, 0.2) 0%, rgba(168, 85, 247, 0.2) 100%)',
              color: '#38bdf8',
              border: '1px solid rgba(56, 189, 248, 0.3)',
              fontWeight: 600,
              fontSize: '0.75rem',
            }}
          />
        </Box>
        <Typography variant="body2" sx={{ color: '#94a3b8' }}>
          Automate supplier product schema harmonization using Google Gemini 2.5 Structured Outputs grounded by enterprise RAG knowledge.
        </Typography>
      </Box>

      {/* Preset Feeds */}
      <Paper sx={{ p: 2.5, mb: 3, bgcolor: '#0f172a', borderRadius: 3, border: '1px solid rgba(255,255,255,0.06)' }}>
        <Typography variant="subtitle2" sx={{ color: '#94a3b8', mb: 1.5, fontWeight: 600 }}>
          SELECT SAMPLE INCOMING SUPPLIER FEED (OR ENTER CUSTOM HEADERS):
        </Typography>
        <Box sx={{ display: 'flex', gap: 1.5, flexWrap: 'wrap' }}>
          {PRESET_FEEDS.map((preset) => (
            <Button
              key={preset.name}
              variant={partnerName === preset.partner ? 'contained' : 'outlined'}
              size="small"
              onClick={() => handleSelectPreset(preset)}
              sx={{
                fontSize: '0.78rem',
                textTransform: 'none',
                borderColor: 'rgba(255,255,255,0.15)',
                color: partnerName === preset.partner ? '#fff' : '#cbd5e1',
                bgcolor: partnerName === preset.partner ? '#0284c7' : 'transparent',
                '&:hover': {
                  borderColor: '#38bdf8',
                  bgcolor: partnerName === preset.partner ? '#0369a1' : 'rgba(56, 189, 248, 0.08)',
                },
              }}
            >
              {preset.name}
            </Button>
          ))}
        </Box>
      </Paper>

      {/* Input Configuration Grid */}
      <Grid container spacing={3} sx={{ mb: 3 }}>
        <Grid item xs={12} md={4}>
          <Paper sx={{ p: 2.5, bgcolor: '#0f172a', borderRadius: 3, border: '1px solid rgba(255,255,255,0.06)', height: '100%' }}>
            <Typography variant="subtitle2" sx={{ color: '#f8fafc', fontWeight: 600, mb: 2 }}>
              Supplier / Source Metadata
            </Typography>
            <TextField
              fullWidth
              size="small"
              label="Supplier / Partner Organization"
              value={partnerName}
              onChange={(e) => setPartnerName(e.target.value)}
              sx={{ mb: 2.5 }}
            />
            <TextField
              fullWidth
              multiline
              rows={4}
              size="small"
              label="Incoming Column Headers (Comma-Separated)"
              value={columnsInput}
              onChange={(e) => setColumnsInput(e.target.value)}
              sx={{
                mb: 2.5,
                '& .MuiInputBase-root': {
                  fontFamily: '"JetBrains Mono", monospace',
                  fontSize: '0.82rem',
                },
              }}
            />
            <Button
              fullWidth
              variant="contained"
              startIcon={<Sparkles size={16} />}
              onClick={handleAnalyze}
              disabled={analyzeMutation.isPending}
              sx={{
                background: 'linear-gradient(135deg, #0284c7 0%, #38bdf8 100%)',
                color: '#fff',
                fontWeight: 600,
                py: 1,
              }}
            >
              {analyzeMutation.isPending ? 'Running Gemini AI Inference...' : 'Analyze with Gemini AI'}
            </Button>
            {analyzeMutation.isPending && <LinearProgress sx={{ mt: 1.5, borderRadius: 1 }} />}
          </Paper>
        </Grid>

        {/* Canonical Knowledge Base Card */}
        <Grid item xs={12} md={8}>
          <Paper sx={{ p: 2.5, bgcolor: '#0f172a', borderRadius: 3, border: '1px solid rgba(255,255,255,0.06)', height: '100%' }}>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="subtitle2" sx={{ color: '#f8fafc', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 1 }}>
                <Database size={16} color="#38bdf8" /> Grounded Canonical Schema Specification
              </Typography>
              <Chip
                label="Ingram Canonical Schema v4.2"
                size="small"
                sx={{ bgcolor: 'rgba(56, 189, 248, 0.1)', color: '#38bdf8', fontSize: '0.72rem' }}
              />
            </Box>

            <Typography variant="body2" sx={{ color: '#94a3b8', mb: 2 }}>
              The Gemini assistant references this internal RAG dictionary to map raw supplier nomenclature to canonical enterprise data contracts:
            </Typography>

            <Grid container spacing={1.5}>
              {(canonicalFields || []).map((field) => (
                <Grid item xs={12} sm={6} key={field.fieldName}>
                  <Box
                    sx={{
                      p: 1.5,
                      borderRadius: 2,
                      bgcolor: 'rgba(255,255,255,0.02)',
                      border: '1px solid rgba(255,255,255,0.04)',
                    }}
                  >
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <Typography variant="caption" sx={{ color: '#f8fafc', fontWeight: 600, fontFamily: '"JetBrains Mono", monospace' }}>
                        {field.fieldName}
                      </Typography>
                      <Chip
                        label={field.dataType}
                        size="small"
                        sx={{ height: 18, fontSize: '0.65rem', bgcolor: 'rgba(255,255,255,0.06)', color: '#94a3b8' }}
                      />
                    </Box>
                    <Typography variant="caption" sx={{ color: '#64748b', display: 'block', fontSize: '0.7rem', mt: 0.5 }}>
                      {field.description}
                    </Typography>
                  </Box>
                </Grid>
              ))}
            </Grid>
          </Paper>
        </Grid>
      </Grid>

      {/* AI Telemetry & Observability Bar */}
      {activeAnalysis && (
        <Paper
          sx={{
            p: 2.5,
            mb: 3,
            bgcolor: '#0f172a',
            borderRadius: 3,
            border: '1px solid rgba(56, 189, 248, 0.25)',
            background: 'linear-gradient(180deg, rgba(56, 189, 248, 0.05) 0%, rgba(15, 23, 42, 0.8) 100%)',
          }}
        >
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 2 }}>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 3 }}>
              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Cpu size={18} color="#38bdf8" />
                <Box>
                  <Typography variant="caption" sx={{ color: '#64748b', display: 'block' }}>MODEL INFERENCE</Typography>
                  <Typography variant="body2" sx={{ color: '#f8fafc', fontWeight: 600 }}>{activeAnalysis.modelUsed}</Typography>
                </Box>
              </Box>

              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Coins size={18} color="#10b981" />
                <Box>
                  <Typography variant="caption" sx={{ color: '#64748b', display: 'block' }}>TOKEN USAGE & COST</Typography>
                  <Typography variant="body2" sx={{ color: '#f8fafc', fontWeight: 600 }}>
                    {activeAnalysis.promptTokens} in / {activeAnalysis.completionTokens} out (${activeAnalysis.estimatedCostUsd.toFixed(6)})
                  </Typography>
                </Box>
              </Box>

              <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                <Clock size={18} color="#f59e0b" />
                <Box>
                  <Typography variant="caption" sx={{ color: '#64748b', display: 'block' }}>INFERENCE LATENCY</Typography>
                  <Typography variant="body2" sx={{ color: '#f8fafc', fontWeight: 600 }}>
                    {activeAnalysis.processingTimeMs.toFixed(0)} ms
                  </Typography>
                </Box>
              </Box>
            </Box>

            <Chip
              icon={<ShieldCheck size={14} color="#38bdf8" />}
              label={activeAnalysis.ragContextSummary}
              sx={{ bgcolor: 'rgba(56, 189, 248, 0.1)', color: '#38bdf8', fontSize: '0.72rem', fontWeight: 600 }}
            />
          </Box>
        </Paper>
      )}

      {/* Approval Success Banner */}
      {approvalSuccess && (
        <Alert
          severity="success"
          icon={<CheckCircle2 size={20} />}
          sx={{ mb: 3, bgcolor: 'rgba(16, 185, 129, 0.12)', color: '#10b981', border: '1px solid rgba(16, 185, 129, 0.3)' }}
        >
          <strong>Mapping Approved & Committed!</strong> The canonical transformation specification for partner '{partnerName}' has been persisted to the extraction engine database.
        </Alert>
      )}

      {/* AI Suggested Mappings Table */}
      {activeAnalysis && (
        <Paper sx={{ p: 3, bgcolor: '#0f172a', borderRadius: 3, border: '1px solid rgba(255,255,255,0.06)' }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
            <Box>
              <Typography variant="h6" sx={{ color: '#f8fafc', fontWeight: 600 }}>
                AI Field Reconciliation & Human Approval Table
              </Typography>
              <Typography variant="caption" sx={{ color: '#94a3b8' }}>
                Review AI-generated mappings. Operators can override target canonical fields or transformations before committing.
              </Typography>
            </Box>

            <Button
              variant="contained"
              startIcon={<Save size={16} />}
              onClick={handleApprove}
              disabled={approveMutation.isPending || approvalSuccess}
              sx={{
                background: 'linear-gradient(135deg, #10b981 0%, #059669 100%)',
                color: '#fff',
                fontWeight: 600,
                px: 3,
              }}
            >
              {approveMutation.isPending ? 'Committing Mapping...' : 'Approve & Save Canonical Mapping'}
            </Button>
          </Box>

          <TableContainer>
            <Table size="small">
              <TableHead sx={{ bgcolor: 'rgba(255,255,255,0.02)' }}>
                <TableRow>
                  <TableCell sx={{ color: '#94a3b8', fontSize: '0.75rem', width: 200 }}>SUPPLIER SOURCE FIELD</TableCell>
                  <TableCell sx={{ color: '#94a3b8', fontSize: '0.75rem', width: 140 }}>CONFIDENCE</TableCell>
                  <TableCell sx={{ color: '#94a3b8', fontSize: '0.75rem', width: 240 }}>TARGET CANONICAL FIELD</TableCell>
                  <TableCell sx={{ color: '#94a3b8', fontSize: '0.75rem', width: 170 }}>TRANSFORMATION</TableCell>
                  <TableCell sx={{ color: '#94a3b8', fontSize: '0.75rem' }}>AI RATIONALE & RAG EVIDENCE</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {editableMappings.map((m, idx) => (
                  <TableRow
                    key={m.sourceColumn}
                    sx={{
                      '&:hover': { bgcolor: 'rgba(255,255,255,0.02)' },
                      borderBottom: '1px solid rgba(255,255,255,0.03)',
                    }}
                  >
                    {/* Source Field */}
                    <TableCell>
                      <Typography
                        variant="body2"
                        sx={{
                          color: '#f8fafc',
                          fontWeight: 600,
                          fontFamily: '"JetBrains Mono", monospace',
                          fontSize: '0.8rem',
                        }}
                      >
                        {m.sourceColumn}
                      </Typography>
                    </TableCell>

                    {/* Confidence */}
                    <TableCell>{getConfidenceBadge(m.confidenceScore)}</TableCell>

                    {/* Target Canonical Select Dropdown */}
                    <TableCell>
                      <Select
                        size="small"
                        fullWidth
                        value={m.targetCanonicalField}
                        onChange={(e) => handleMappingChange(idx, e.target.value)}
                        sx={{
                          fontSize: '0.78rem',
                          fontFamily: '"JetBrains Mono", monospace',
                          bgcolor: 'rgba(255,255,255,0.03)',
                          color: '#38bdf8',
                        }}
                      >
                        {(canonicalFields || []).map((cf) => (
                          <MenuItem key={cf.fieldName} value={cf.fieldName} sx={{ fontSize: '0.78rem' }}>
                            {cf.fieldName} ({cf.dataType})
                          </MenuItem>
                        ))}
                      </Select>
                    </TableCell>

                    {/* Transformation Select */}
                    <TableCell>
                      <Select
                        size="small"
                        fullWidth
                        value={m.transformationRule}
                        onChange={(e) => handleTransformChange(idx, e.target.value)}
                        sx={{ fontSize: '0.75rem', bgcolor: 'rgba(255,255,255,0.03)' }}
                      >
                        <MenuItem value="Direct">Direct</MenuItem>
                        <MenuItem value="Trim">Trim Whitespace</MenuItem>
                        <MenuItem value="ParseCurrency">Parse Currency ($)</MenuItem>
                        <MenuItem value="ToInteger">Cast To Integer</MenuItem>
                        <MenuItem value="ToDecimal">Cast To Decimal</MenuItem>
                        <MenuItem value="UpperCase">To UpperCase</MenuItem>
                      </Select>
                    </TableCell>

                    {/* AI Rationale */}
                    <TableCell>
                      <Typography variant="caption" sx={{ color: '#cbd5e1', fontSize: '0.75rem' }}>
                        {m.aiRationale}
                      </Typography>
                      {m.isUserOverridden && (
                        <Chip
                          label="Manual Override"
                          size="small"
                          sx={{
                            ml: 1,
                            height: 18,
                            fontSize: '0.65rem',
                            bgcolor: 'rgba(245, 158, 11, 0.15)',
                            color: '#f59e0b',
                            fontWeight: 600,
                          }}
                        />
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      )}
    </Box>
  );
};
