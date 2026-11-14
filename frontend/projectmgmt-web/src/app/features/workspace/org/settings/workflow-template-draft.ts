import { ManagementType, SaveWorkflowTemplateRequest, WorkflowStatusDef, WorkflowTemplate } from '../../data/workspace.models';

export const MAX_TEMPLATE_STATUSES = 20;

export interface TemplateDraft {
  /** null khi tạo mẫu mới. */
  id: string | null;
  name: string;
  managementType: ManagementType;
  statuses: WorkflowStatusDef[];
}

export function blankDraft(): TemplateDraft {
  return {
    id: null,
    name: '',
    managementType: 'Scrum',
    statuses: [
      { name: 'To Do', category: 'ToDo' },
      { name: 'In Progress', category: 'InProgress' },
      { name: 'Done', category: 'Done' }
    ]
  };
}

/** Bản sao để sửa: mẫu của tổ chức giữ id; mẫu có sẵn thành mẫu mới tên "(bản sao)". */
export function draftFrom(template: WorkflowTemplate): TemplateDraft {
  return {
    id: template.isPreset ? null : template.id,
    name: template.isPreset ? `${template.name} (bản sao)` : template.name,
    managementType: template.managementType,
    statuses: template.statuses.map(status => ({ ...status }))
  };
}

export function moveStatus(statuses: readonly WorkflowStatusDef[], index: number, delta: -1 | 1): WorkflowStatusDef[] {
  const target = index + delta;
  if (target < 0 || target >= statuses.length) return [...statuses];
  const next = [...statuses];
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}

/** Cùng ràng buộc với server: tên 2–60, 2–20 trạng thái không trùng, có ít nhất một "Cần làm" và một "Hoàn thành". */
export function validateDraft(draft: TemplateDraft): { name: string; statuses: string[]; form: string } {
  const name = draft.name.trim();
  const lowered = draft.statuses.map(status => status.name.trim().toLowerCase());
  const statuses = draft.statuses.map((status, index) => {
    const value = status.name.trim();
    if (!value) return 'Nhập tên trạng thái.';
    if (value.length > 40) return 'Tối đa 40 ký tự.';
    return lowered.indexOf(value.toLowerCase()) !== index ? 'Trùng với trạng thái khác.' : '';
  });
  let form = '';
  if (draft.statuses.length < 2) form = 'Mẫu cần ít nhất 2 trạng thái.';
  else if (draft.statuses.length > MAX_TEMPLATE_STATUSES) form = `Tối đa ${MAX_TEMPLATE_STATUSES} trạng thái.`;
  else if (!draft.statuses.some(status => status.category === 'ToDo') || !draft.statuses.some(status => status.category === 'Done')) {
    form = 'Cần ít nhất một trạng thái "Cần làm" và một trạng thái "Hoàn thành".';
  }
  return {
    name: name.length < 2 ? 'Nhập tên mẫu (ít nhất 2 ký tự).' : name.length > 60 ? 'Tên mẫu tối đa 60 ký tự.' : '',
    statuses,
    form
  };
}

export function isDraftValid(draft: TemplateDraft): boolean {
  const result = validateDraft(draft);
  return !result.name && !result.form && result.statuses.every(error => !error);
}

export function toRequest(draft: TemplateDraft): SaveWorkflowTemplateRequest {
  return {
    name: draft.name.trim(),
    managementType: draft.managementType,
    statuses: draft.statuses.map(status => ({ name: status.name.trim(), category: status.category }))
  };
}
