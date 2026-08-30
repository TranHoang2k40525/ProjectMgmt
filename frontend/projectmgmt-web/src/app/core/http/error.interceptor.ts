import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { ApiError } from '../models/api-error.model';

export const errorInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(catchError((error: unknown) => throwError(() => normalizeApiError(error))));

function normalizeApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, code: 'client.unexpected', title: 'Unexpected client error' };
  }

  const body = typeof error.error === 'object' && error.error !== null
    ? error.error as Record<string, unknown>
    : {};

  return {
    status: error.status,
    code: stringValue(body['code']) ?? 'http.error',
    title: stringValue(body['title']) ?? error.statusText ?? 'Request failed',
    detail: stringValue(body['detail']) ?? error.message,
    traceId: stringValue(body['traceId']),
    errors: isErrorDictionary(body['errors']) ? body['errors'] : undefined
  };
}

function stringValue(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}

function isErrorDictionary(value: unknown): value is Record<string, readonly string[]> {
  return typeof value === 'object'
    && value !== null
    && Object.values(value).every(item => Array.isArray(item) && item.every(message => typeof message === 'string'));
}
