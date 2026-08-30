export interface ApiError {
  status: number;
  code: string;
  title: string;
  detail?: string;
  traceId?: string;
  errors?: Readonly<Record<string, readonly string[]>>;
}
