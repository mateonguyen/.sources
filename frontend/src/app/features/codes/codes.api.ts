import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface CodeValueDto {
  id: number;
  codeId: number;
  value: string;
  name: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
}

export interface CodeDto {
  id: number;
  code: string;
  name: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
  values: CodeValueDto[];
}

export interface UpsertCodeRequest {
  code: string;
  name: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
}

export interface UpsertCodeValueRequest {
  value: string;
  name: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class CodesApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(includeInactive = false): Promise<CodeDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<CodeDto[]>>(
        `${API_BASE_URL}/codes?includeInactive=${includeInactive}`,
      ),
    ).then((response) => response.data);
  }

  getById(id: number): Promise<CodeDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<CodeDto>>(`${API_BASE_URL}/codes/${id}`),
    ).then((response) => response.data);
  }

  getByCode(code: string): Promise<CodeDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<CodeDto>>(
        `${API_BASE_URL}/codes/by-code/${encodeURIComponent(code)}`,
      ),
    ).then((response) => response.data);
  }

  create(payload: UpsertCodeRequest): Promise<CodeDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<CodeDto>>(
        `${API_BASE_URL}/codes`,
        payload,
      ),
    ).then((response) => response.data);
  }

  update(id: number, payload: UpsertCodeRequest): Promise<CodeDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<CodeDto>>(
        `${API_BASE_URL}/codes/${id}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/codes/${id}`,
      ),
    ).then(() => undefined);
  }

  getValues(codeId: number, includeInactive = false): Promise<CodeValueDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<CodeValueDto[]>>(
        `${API_BASE_URL}/codes/${codeId}/values?includeInactive=${includeInactive}`,
      ),
    ).then((response) => response.data);
  }

  createValue(
    codeId: number,
    payload: UpsertCodeValueRequest,
  ): Promise<CodeValueDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<CodeValueDto>>(
        `${API_BASE_URL}/codes/${codeId}/values`,
        payload,
      ),
    ).then((response) => response.data);
  }

  updateValue(
    codeId: number,
    valueId: number,
    payload: UpsertCodeValueRequest,
  ): Promise<CodeValueDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<CodeValueDto>>(
        `${API_BASE_URL}/codes/${codeId}/values/${valueId}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  deleteValue(codeId: number, valueId: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/codes/${codeId}/values/${valueId}`,
      ),
    ).then(() => undefined);
  }
}
