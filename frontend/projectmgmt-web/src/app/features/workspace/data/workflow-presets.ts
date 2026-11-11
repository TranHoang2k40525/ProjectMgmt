import { ManagementType, WorkflowStatusDef, WorkflowTemplate } from './workspace.models';

const todo = (name: string): WorkflowStatusDef => ({ name, category: 'ToDo' });
const doing = (name: string): WorkflowStatusDef => ({ name, category: 'InProgress' });
const done = (name: string): WorkflowStatusDef => ({ name, category: 'Done' });

/** Mẫu có sẵn — cùng tên trạng thái với `ProjectCustomizationController.ApplyTemplate` của backend. */
export const PRESET_TEMPLATES: readonly { id: string; name: string; managementType: ManagementType; statuses: WorkflowStatusDef[] }[] = [
  { id: 'preset-scrum-std', name: 'Scrum chuẩn', managementType: 'Scrum', statuses: [todo('To Do'), doing('In Progress'), doing('Code Review'), done('Done')] },
  { id: 'preset-agile-qa', name: 'Agile + QA', managementType: 'Scrum', statuses: [todo('To Do'), doing('In Progress'), doing('Code Review'), doing('QA Testing'), done('Done')] },
  { id: 'preset-kanban-simple', name: 'Kanban đơn giản', managementType: 'Kanban', statuses: [todo('To Do'), doing('In Progress'), done('Done')] },
  { id: 'preset-enterprise', name: 'Enterprise', managementType: 'Scrum', statuses: [todo('Backlog'), doing('Spec Review'), doing('In Progress'), doing('Code Review'), doing('QA Verification'), done('Done')] }
];

/** Mẫu có sẵn dạng {@link WorkflowTemplate}; `defaultId` là mẫu được chọn sẵn khi tạo dự án. */
export function presetWorkflowTemplates(defaultId: string = PRESET_TEMPLATES[0].id): WorkflowTemplate[] {
  return PRESET_TEMPLATES.map(preset => ({ ...structuredClone(preset), orgId: null, isPreset: true, isDefault: preset.id === defaultId }));
}
