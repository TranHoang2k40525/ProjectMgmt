import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AiBreakdownDto, AiPromptTemplateDto } from './ai-breakdown-api.models';

@Injectable({ providedIn: 'root' })
export class AiBreakdownApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1/ai`;

  generate(issueId: string, projectContext?: string): Observable<AiBreakdownDto> {
    return this.http.post<AiBreakdownDto>(`${this.baseUrl}/breakdown/generate`, {
      issueId,
      projectContext
    });
  }

  getGeneration(generationId: string): Observable<AiBreakdownDto> {
    return this.http.get<AiBreakdownDto>(`${this.baseUrl}/breakdown/${generationId}`);
  }

  saveFeedback(generationId: string, request: Partial<AiBreakdownDto>): Observable<AiBreakdownDto> {
    return this.http.post<AiBreakdownDto>(`${this.baseUrl}/breakdown/${generationId}/feedback`, request);
  }

  apply(generationId: string, selectedSubTasks: Partial<AiBreakdownDto>[]): Observable<AiBreakdownDto> {
    return this.http.post<AiBreakdownDto>(`${this.baseUrl}/breakdown/${generationId}/apply`, {
      selectedSubTasks
    });
  }

  getPromptTemplates(): Observable<AiPromptTemplateDto> {
    return this.http.get<AiPromptTemplateDto>(`${this.baseUrl}/prompt-templates`);
  }

  activatePromptTemplate(templateId: string): Observable<AiPromptTemplateDto> {
    return this.http.put<AiPromptTemplateDto>(`${this.baseUrl}/prompt-templates/${templateId}/activate`, {});
  }
}
