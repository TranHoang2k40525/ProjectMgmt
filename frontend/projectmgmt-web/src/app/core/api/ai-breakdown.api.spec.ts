import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { environment } from '../../../environments/environment';
import { AiBreakdownApi } from './ai-breakdown.api';

describe('AiBreakdownApi', () => {
  let api: AiBreakdownApi;
  let http: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/v1/ai`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    api = TestBed.inject(AiBreakdownApi);
    http = TestBed.inject(HttpTestingController);
  });

  it('gửi yêu cầu phân rã AI từ Issue ID', () => {
    const issueId = 'issue-100';
    api.generate(issueId, 'Sprint 2 context').subscribe();
    const req = http.expectOne(`${baseUrl}/breakdown/generate`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ issueId, projectContext: 'Sprint 2 context' });
    req.flush({ success: true, generationId: 'gen-1', status: 'Completed', suggestedSubTasks: [] });
    http.verify();
  });

  it('đọc chi tiết kết quả phân rã AI', () => {
    const generationId = 'gen-1';
    api.getGeneration(generationId).subscribe();
    const req = http.expectOne(`${baseUrl}/breakdown/${generationId}`);
    expect(req.request.method).toBe('GET');
    req.flush({ success: true, generationId, status: 'Completed' });
    http.verify();
  });

  it('áp dụng các task đề xuất để sinh issue thật', () => {
    const generationId = 'gen-1';
    const selected = [{ title: 'Sub-task 1', estimatePoints: 2 }];
    api.apply(generationId, selected).subscribe();
    const req = http.expectOne(`${baseUrl}/breakdown/${generationId}/apply`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ selectedSubTasks: selected });
    req.flush({ success: true, applied: true, appliedCount: 1 });
    http.verify();
  });

  it('lấy danh sách và kích hoạt prompt template', () => {
    api.getPromptTemplates().subscribe();
    const reqList = http.expectOne(`${baseUrl}/prompt-templates`);
    expect(reqList.request.method).toBe('GET');
    reqList.flush({ success: true, items: [] });

    api.activatePromptTemplate('tmpl-1').subscribe();
    const reqAct = http.expectOne(`${baseUrl}/prompt-templates/tmpl-1/activate`);
    expect(reqAct.request.method).toBe('PUT');
    reqAct.flush({ success: true });
    http.verify();
  });
});
