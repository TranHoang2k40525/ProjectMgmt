import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IdentityService } from '../../core/services/identity.service';
import { AiModelConfig, AiGenLogModel } from '../../core/mocks/identity-mock-db';

@Component({
  selector: 'app-ai-governance-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-governance-page.html',
  styleUrls: ['./ai-governance-page.scss']
})
export class AiGovernancePageComponent implements OnInit {
  private identityService = inject(IdentityService);

  readonly models = signal<AiModelConfig[]>([]);
  readonly logs = signal<AiGenLogModel[]>([]);
  readonly successMsg = signal<string | null>(null);

  ngOnInit(): void {
    this.identityService.getAiModels().subscribe(res => this.models.set(res));
    this.identityService.getAiLogs().subscribe(res => this.logs.set(res));
  }

  toggleModelActive(m: AiModelConfig): void {
    m.isEnabled = !m.isEnabled;
    this.successMsg.set(`Đã ${m.isEnabled ? 'kích hoạt' : 'tạm dừng'} mô hình ${m.modelName}`);
  }

  get TotalTokenUsed(): number {
    return this.logs().reduce((acc, l) => acc + l.tokensUsed, 0);
  }

  get TotalCostEstimated(): number {
    return this.logs().reduce((acc, l) => acc + l.costEstimate, 0);
  }
}
