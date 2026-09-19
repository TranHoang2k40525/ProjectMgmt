import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface BacklogIssue {
  id: string;
  key: string;
  title: string;
  tag: string;
  status: string;
  assignee: string;
  storyPoints: number;
  aiBreakdown?: boolean;
  description: string;
  subtasks: { id: string; title: string; done: boolean }[];
}

@Component({
  selector: 'app-backlog-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './backlog-page.html',
  styleUrl: './backlog-page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BacklogPage {
  searchQuery = '';

  sprint1Items = signal<BacklogIssue[]>([
    {
      id: '1',
      key: 'SCRUMAI-155',
      title: '[DEVOPS] Dựng Docker Compose local MySQL + Ollama',
      tag: 'CI/CD, Environment',
      status: 'In Progress',
      assignee: 'TH',
      storyPoints: 5,
      description: 'Cấu hình môi trường phát triển cục bộ với docker-compose.yml phục vụ chạy database MySQL 8.0 và LLM Ollama phục vụ đồ án Scrum AI tại phòng Lab HUCE. Yêu cầu mount volume dữ liệu kiên cố và cấu hình network nội bộ.',
      subtasks: [
        { id: 's1', title: 'Tạo file docker-compose.yml và file cấu hình .env', done: true },
        { id: 's2', title: 'Thiết lập container Ollama pull model qwen2.5-coder', done: false },
        { id: 's3', title: 'Viết script kiểm tra healthcheck MySQL tự động kết nối', done: false }
      ]
    },
    {
      id: '2',
      key: 'SCRUMAI-158',
      title: '[TEST/PROCESS] Xây test suite và quy trình kiểm thử',
      tag: 'QA, Security, Perf',
      status: 'In Progress',
      assignee: 'HH',
      storyPoints: 3,
      description: 'Thiết lập E2E Playwright test suite và Vitest unit tests cho các phân hệ SSO, Scoped RBAC và Sprint Auto-Pilot.',
      subtasks: [
        { id: 's4', title: 'Viết test case login thành công với JWT token', done: true },
        { id: 's5', title: 'Kiểm thử responsive trên viewports mobile và desktop', done: true }
      ]
    },
    {
      id: '3',
      key: 'SCRUMAI-159',
      title: '[AI/TEST] Thiết kế tập dữ liệu test cho AI Auto Task Breakdown',
      tag: 'AI Dataset, Cleaning',
      status: 'In Progress',
      assignee: 'HH',
      storyPoints: 5,
      description: 'Thu thập 50 đề cương đồ án tốt nghiệp K65 mẫu để huấn luyện prompt phân rã Epics và User Stories.',
      subtasks: [
        { id: 's6', title: 'Tiền xử lý file PDF đề cương đồ án tốt nghiệp', done: false }
      ]
    }
  ]);

  sprint2Items = signal<BacklogIssue[]>([
    {
      id: '4',
      key: 'SCRUMAI-160',
      title: '[BE] Đăng ký/đăng nhập, JWT Refresh Token',
      tag: 'Identity, Authentication',
      status: 'To Do',
      assignee: 'TH',
      storyPoints: 8,
      aiBreakdown: true,
      description: 'Phát triển backend C# .NET 10 Web API phục vụ xác thực người dùng, OTP email và mã hóa mật khẩu Scoped RBAC.',
      subtasks: [
        { id: 's7', title: 'Cấu hình JWT Token Bearer Authentication', done: false }
      ]
    },
    {
      id: '5',
      key: 'SCRUMAI-161',
      title: '[DB/BE] Entity/migration Role Permission',
      tag: 'Identity, Database',
      status: 'To Do',
      assignee: 'TH',
      storyPoints: 5,
      aiBreakdown: true,
      description: 'Thiết kế bảng CSDL SQL Server lưu trữ Roles, Claims và Permissions cho người dùng HUCE.',
      subtasks: []
    }
  ]);

  selectedIssue = signal<BacklogIssue | null>(this.sprint1Items()[0]);

  selectIssue(issue: BacklogIssue): void {
    this.selectedIssue.set(issue);
  }

  closeDrawer(): void {
    this.selectedIssue.set(null);
  }

  toggleAiModal(): void {
    alert('🤖 AI Assistant: Đang tự động phân rã 12 tasks trong Backlog thành Epics & Stories theo chuẩn Agile!');
  }

  generateAiSubtasks(): void {
    const issue = this.selectedIssue();
    if (issue) {
      const updated = { ...issue };
      updated.subtasks.push({
        id: 's_ai_' + Date.now(),
        title: '✨ AI Auto-generated: Kiểm tra bảo mật SSL 256-bit và JWT Refresh Token',
        done: false
      });
      this.selectedIssue.set(updated);
    }
  }
}
