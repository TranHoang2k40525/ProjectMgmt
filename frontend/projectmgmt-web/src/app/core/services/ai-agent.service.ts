import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IdentityService } from './identity.service';
import { TOKEN_STORE, AuthSession } from '../auth/token-store';
import { AccountApi } from '../api/account.api';

export interface ProposedSubTaskDto {
  id: string;
  title: string;
  role: string;
  estimatedHours: number;
  storyPoints?: number;
  priority?: string;
  status?: string;
  description?: string;
  assigneeUserId?: string | null;
  assigneeName?: string | null;
  acceptanceCriteria?: string[];
  documentCitations?: string[];
}

export interface ProposedTaskDto {
  id: string;
  sourceBacklogIssueId?: string | null;
  sourceBacklogIssueKey?: string | null;
  epicId?: string | null;
  epicTitle?: string | null;
  title: string;
  description: string;
  type: string;
  priority: string;
  status: string;
  isReady: boolean;
  storyPoints: number;
  estimatedHours: number;
  remainingHours: number;
  assigneeUserId?: string | null;
  assigneeName?: string | null;
  tags?: string[];
  suggestedSkills: string[];
  acceptanceCriteria: string[];
  documentCitations: string[];
  documentId?: string | null;
  aiRationale?: string | null;
  subtasks: ProposedSubTaskDto[];
}

export interface AiMemoryAnchorDto {
  anchorKey: string;
  turnIndex: number;
  category: string;
  title: string;
  keywords: string;
  summary: string;
  detailContent: string;
  createdAtUtc?: string;
}

export interface AiTurnTraceItemDto {
  kind: string;
  actionVerb: string;
  target: string;
  summary: string;
  detail?: string | null;
  durationMs?: number | null;
  status: string;
}

export interface AiConversationTurnDto {
  id: string;
  role: 'user' | 'assistant' | string;
  content: string;
  fullContent?: string | null;
  fileName?: string | null;
  intent?: string | null;
  actionType?: string | null;
  taskCount?: number;
  totalStoryPoints?: number;
  thinkingSummary?: string | null;
  durationMs?: number;
  thoughts?: string | null;
  referencedMemoryAnchors?: string[];
  traceItems?: AiTurnTraceItemDto[];
  timestampUtc?: string;
}

export type AiConversationIntent =
  | 'sprint_plan'
  | 'backlog_plan'
  | 'refine_proposal'
  | 'project_qa'
  | 'export_excel'
  | 'push_backlog'
  | 'apply_sprint'
  | 'out_of_scope_refusal';

export interface AiSprintProposalDto {
  sessionId: string;
  organizationId?: string | null;
  projectId: string;
  userId?: string | null;
  targetSprintId?: string | null;
  sprintGoal: string;
  rationale: string;
  tags?: string[];
  totalEstimatedHours: number;
  totalStoryPoints: number;
  workflowName?: string | null;
  initialStatus?: string | null;
  appliedWorkflowStatuses?: string[];
  appliedPriorities?: string[];
  appliedIssueTypes?: string[];
  appliedLabels?: string[];
  tasks: ProposedTaskDto[];
  createdAtUtc?: string;
  intent?: AiConversationIntent | string | null;
  assistantReply?: string | null;
  recommendedTarget?: 'sprint' | 'backlog' | string | null;
  triggerAction?: 'none' | 'export_excel' | 'push_backlog' | 'apply_sprint' | string | null;
  conversationHistory?: AiConversationTurnDto[];
  indexedMemories?: AiMemoryAnchorDto[];
  totalConversationTurns?: number;
  hasOlderMessages?: boolean;
  oldestLoadedTurnIndex?: number | null;
}

export interface AiConversationMessagesPageDto {
  sessionId: string;
  totalCount: number;
  oldestLoadedIndex?: number | null;
  hasOlderMessages: boolean;
  messages: AiConversationTurnDto[];
}

export interface AiStepProgressEvent {
  stepIndex: number;
  totalSteps: number;
  stepName: string;
  labelVi: string;
  percent: number;
  detail?: string;
}

export interface AiToolCallEvent {
  toolName: string;
  toolArguments?: unknown;
  summaryVi: string;
  durationMs?: number;
  actionVerb?: string | null;
  targetLabel?: string | null;
  detailPreview?: string | null;
}

export interface AiApplySprintProposalResponse {
  success: boolean;
  errorCode?: string;
  message: string;
  sprintId: string;
  sprintName: string;
  sprintGoal: string;
  createdIssueCount: number;
  createdTaskCount: number;
  createdSubtaskCount: number;
  movedFromBacklogCount?: number;
  totalStoryPoints: number;
}

export interface AiTokenUsageSummary {
  totalTokens: number;
  promptTokens: number;
  completionTokens: number;
  totalCalls: number;
  successfulCalls: number;
  successRate: number;
  estimatedCostUsd: number;
  modelBreakdown: Record<string, number>;
  recentHistory: Array<{
    sessionId: string;
    model: string;
    promptTokens: number;
    completionTokens: number;
    durationMs: number;
    success: boolean;
    timestampUtc: string;
    operationName?: string;
  }>;
}

export interface AiSessionSummary {
  sessionId: string;
  projectId: string;
  createdByUserId: string;
  createdByName: string;
  sprintGoal: string;
  taskCount: number;
  totalStoryPoints: number;
  model: string;
  createdAtUtc: string;
  title?: string | null;
  turnCount?: number;
  memoryCount?: number;
  updatedAtUtc?: string | null;
  lastIntent?: string | null;
}

export interface AiStreamCallbacks {
  onSessionInit?: (data: { sessionId: string; projectId: string; actorName: string }) => void;
  onStepProgress?: (step: AiStepProgressEvent) => void;
  onToolCall?: (tool: AiToolCallEvent) => void;
  onThoughtDelta?: (delta: string) => void;
  onProposalReady?: (proposal: AiSprintProposalDto) => void;
  onCompleted?: (data: { sessionId: string; totalDurationMs: number; taskCount: number; totalStoryPoints: number }) => void;
  onError?: (err: { code: string; message: string }) => void;
}

@Injectable({ providedIn: 'root' })
export class AiAgentService {
  private readonly http = inject(HttpClient);
  private readonly identity = inject(IdentityService);
  private readonly tokenStore = inject(TOKEN_STORE);
  private readonly accountApi = inject(AccountApi);
  private readonly baseUrl = `${environment.apiBaseUrl}/ai/agent`;

  private async getValidAccessToken(forceRefresh = false): Promise<string | null> {
    let token = this.tokenStore.getAccessToken() ?? this.identity.authState().token;
    if (!forceRefresh && token) {
      return token;
    }
    const refreshToken = this.tokenStore.getRefreshToken();
    if (refreshToken) {
      try {
        const result = await firstValueFrom(this.accountApi.refreshToken(refreshToken));
        if (result?.accessToken) {
          const current = this.tokenStore.getSession();
          const nextSession: AuthSession = {
            accessToken: result.accessToken,
            refreshToken: result.refreshToken ?? current?.refreshToken ?? refreshToken,
            accessTokenExpiresAt: result.accessTokenExpiresAt ?? null,
            refreshTokenExpiresAt: result.refreshTokenExpiresAt ?? null,
            userId: result.userId ?? current?.userId ?? '',
            email: result.email ?? current?.email ?? '',
            fullName: result.fullName ?? current?.fullName ?? '',
            avatarUrl: result.avatarUrl ?? current?.avatarUrl ?? null,
            roles: result.roles ?? current?.roles ?? []
          };
          this.tokenStore.setSession(nextSession);
          this.identity.authState.update(state => ({
            ...state,
            token: result.accessToken ?? null,
            isAuthenticated: true
          }));
          return result.accessToken;
        }
      } catch {
        // Refresh token failed or expired
      }
    }
    return token;
  }

  async streamDecomposeSprint(
    params: {
      projectId: string;
      text?: string;
      prompt?: string;
      file?: File | null;
      targetSprintId?: string | null;
      model?: string;
      sessionId?: string | null;
    },
    callbacks: AiStreamCallbacks,
    abortSignal?: AbortSignal
  ): Promise<void> {
    const formData = new FormData();
    formData.append('projectId', params.projectId);
    if (params.sessionId) {
      formData.append('sessionId', params.sessionId);
    }
    if (params.text) {
      formData.append('text', params.text);
    }
    if (params.prompt) {
      formData.append('prompt', params.prompt);
    }
    if (params.targetSprintId) {
      formData.append('targetSprintId', params.targetSprintId);
    }
    if (params.file) {
      formData.append('file', params.file, params.file.name);
    }
    if (params.model) {
      formData.append('model', params.model);
    }

    let token = await this.getValidAccessToken();
    const headers: Record<string, string> = {
      Accept: 'text/event-stream',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    let response = await fetch(`${this.baseUrl}/decompose/stream`, {
      method: 'POST',
      headers,
      body: formData,
      signal: abortSignal,
    });

    if (response.status === 401) {
      const refreshedToken = await this.getValidAccessToken(true);
      if (refreshedToken && refreshedToken !== token) {
        headers['Authorization'] = `Bearer ${refreshedToken}`;
        response = await fetch(`${this.baseUrl}/decompose/stream`, {
          method: 'POST',
          headers,
          body: formData,
          signal: abortSignal,
        });
      }
    }

    if (!response.ok) {
      callbacks.onError?.({
        code: `HTTP_${response.status}`,
        message: response.status === 401
          ? 'Phiên làm việc đã hết hạn hoặc không có quyền truy cập (HTTP 401). Vui lòng đăng nhập lại.'
          : `Không thể kết nối luồng AI Agent (HTTP ${response.status}).`,
      });
      return;
    }

    if (!response.body) {
      callbacks.onError?.({
        code: 'EMPTY_STREAM',
        message: 'Trình duyệt không hỗ trợ đọc luồng dữ liệu SSE.',
      });
      return;
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder('utf-8');
    let buffer = '';

    while (true) {
      const { value, done } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const blocks = buffer.split('\n\n');
      buffer = blocks.pop() ?? '';

      for (const block of blocks) {
        const trimmed = block.trim();
        if (!trimmed) continue;

        let eventType = 'message';
        const dataLines: string[] = [];

        for (const line of trimmed.split('\n')) {
          if (line.startsWith('event:')) {
            eventType = line.slice('event:'.length).trim();
          } else if (line.startsWith('data:')) {
            dataLines.push(line.slice('data:'.length).trim());
          }
        }

        const rawData = dataLines.join('\n');
        if (!rawData) continue;

        try {
          const parsed = JSON.parse(rawData);
          this.dispatchEvent(eventType, parsed, callbacks);
        } catch {
          // Ignore malformed chunk
        }
      }
    }
  }

  private dispatchEvent(eventType: string, payload: any, callbacks: AiStreamCallbacks): void {
    switch (eventType) {
      case 'session_init':
        callbacks.onSessionInit?.(payload);
        break;
      case 'step_progress':
        callbacks.onStepProgress?.(payload);
        break;
      case 'tool_call':
        callbacks.onToolCall?.(payload);
        break;
      case 'thought_stream':
        if (payload?.delta) {
          callbacks.onThoughtDelta?.(payload.delta);
        }
        break;
      case 'proposal_ready':
        callbacks.onProposalReady?.(payload);
        break;
      case 'completed':
        callbacks.onCompleted?.(payload);
        break;
      case 'error':
        callbacks.onError?.(payload);
        break;
    }
  }

  regenerateSingleTask(sessionId: string, taskId: string, feedback: string): Observable<{ updatedTask: ProposedTaskDto }> {
    return this.http.post<{ updatedTask: ProposedTaskDto }>(`${this.baseUrl}/regenerate-task`, {
      sessionId,
      taskId,
      feedback,
    });
  }

  regenerateFullProposal(sessionId: string, feedback: string): Observable<AiSprintProposalDto> {
    return this.http.post<AiSprintProposalDto>(`${this.baseUrl}/regenerate-all`, {
      sessionId,
      feedback,
    });
  }

  updateStagedProposal(sessionId: string, proposal: AiSprintProposalDto): Observable<AiSprintProposalDto> {
    return this.http.put<AiSprintProposalDto>(`${this.baseUrl}/proposal/${encodeURIComponent(sessionId)}`, {
      sprintGoal: proposal.sprintGoal,
      rationale: proposal.rationale,
      targetSprintId: proposal.targetSprintId,
      tasks: proposal.tasks,
    });
  }

  getStagedProposal(sessionId: string, tailLimit?: number): Observable<AiSprintProposalDto> {
    let params = new HttpParams();
    if (tailLimit && tailLimit > 0) {
      params = params.set('tailLimit', String(tailLimit));
    }
    return this.http.get<AiSprintProposalDto>(`${this.baseUrl}/proposal/${encodeURIComponent(sessionId)}`, { params });
  }

  getSessionMessages(
    sessionId: string,
    projectId: string,
    beforeIndex?: number | null,
    limit = 12
  ): Observable<AiConversationMessagesPageDto> {
    let params = new HttpParams()
      .set('projectId', projectId)
      .set('limit', String(limit));
    if (beforeIndex !== undefined && beforeIndex !== null) {
      params = params.set('beforeIndex', String(beforeIndex));
    }
    return this.http.get<AiConversationMessagesPageDto>(
      `${this.baseUrl}/sessions/${encodeURIComponent(sessionId)}/messages`,
      { params }
    );
  }

  applyProposal(payload: {
    sessionId: string;
    projectId: string;
    targetSprintId?: string | null;
    sprintGoal: string;
    tasks: ProposedTaskDto[];
    pushToBacklog?: boolean;
  }): Observable<AiApplySprintProposalResponse> {
    return this.http.post<AiApplySprintProposalResponse>(`${this.baseUrl}/apply`, payload);
  }

  getTokenUsage(projectId?: string): Observable<AiTokenUsageSummary> {
    let params = new HttpParams();
    if (projectId) {
      params = params.set('projectId', projectId);
    }
    return this.http.get<AiTokenUsageSummary>(`${this.baseUrl}/token-usage`, { params });
  }

  getProjectSessions(projectId: string): Observable<AiSessionSummary[]> {
    const params = new HttpParams().set('projectId', projectId);
    return this.http.get<AiSessionSummary[]>(`${this.baseUrl}/sessions`, { params });
  }

  deleteSession(sessionId: string, projectId: string): Observable<{ success: boolean; message: string }> {
    const params = new HttpParams().set('projectId', projectId);
    return this.http.delete<{ success: boolean; message: string }>(
      `${this.baseUrl}/sessions/${encodeURIComponent(sessionId)}`,
      { params }
    );
  }

  clearCache(sessionId?: string, projectId?: string): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(`${this.baseUrl}/clear-cache`, {
      sessionId,
      projectId,
    });
  }
}
