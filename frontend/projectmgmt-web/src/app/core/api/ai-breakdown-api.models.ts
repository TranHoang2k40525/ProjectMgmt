import { ApiResult } from './account-api.models';

export interface AiBreakdownDto extends ApiResult {
  generationId?: string | null;
  issueId?: string | null;
  parentIssueId?: string | null;
  projectId?: string | null;
  suggestedTaskId?: string | null;
  createdIssueId?: string | null;
  issueKey?: string | null;
  status?: string | null;
  modelVersion?: string | null;
  promptVersion?: string | null;
  storyTitle?: string | null;
  storyDescription?: string | null;
  projectContext?: string | null;
  tempId?: string | null;
  title?: string | null;
  description?: string | null;
  estimatedHours?: number | null;
  estimatePoints?: number | null;
  acceptanceCriteria?: string[] | null;
  suggestedSkills?: string[] | null;
  userAction?: string | null;
  finalSummary?: string | null;
  finalDescription?: string | null;
  finalAcceptanceCriteria?: string[] | null;
  finalEstimatePoints?: number | null;
  editDistanceRatio?: number | null;
  rejectReason?: string | null;
  feedbackLogged?: boolean | null;
  applied?: boolean | null;
  appliedCount?: number | null;
  suggestedSubTasks?: AiBreakdownDto[] | null;
  selectedSubTasks?: AiBreakdownDto[] | null;
  createdSubTasks?: AiBreakdownDto[] | null;
}

export interface AiPromptTemplateDto extends ApiResult {
  promptId?: string | null;
  code?: string | null;
  version?: number | null;
  taskType?: string | null;
  language?: string | null;
  systemPrompt?: string | null;
  userTemplate?: string | null;
  jsonSchema?: string | null;
  isActive?: boolean | null;
  createdAt?: string | null;
  items?: AiPromptTemplateDto[] | null;
}
