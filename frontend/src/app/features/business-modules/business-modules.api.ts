import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export type BizValue = string | number | boolean | null;

export interface BizRecordDto {
  id: number;
  donViId: number;
  kyBaoCaoCode: string | null;
  [key: string]: BizValue;
}

@Injectable({ providedIn: 'root' })
export class BusinessModulesApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(
    endpoint: string,
    kyCode?: string,
    historyMode = false,
  ): Promise<BizRecordDto[]> {
    const year = kyCode?.match(/^\d{4}/)?.[0];
    const params = kyCode
      ? {
          kyCode,
          kyBaoCaoCode: kyCode,
          mode: historyMode ? 'history' : 'live',
          ...(year ? { nam: year } : {}),
        }
      : undefined;

    return firstValueFrom(
      this.httpClient.get<ApiResponse<BizRecordDto[]>>(
        `${API_BASE_URL}/${endpoint}`,
        { params },
      ),
    ).then((response) => response.data);
  }

  getById(endpoint: string, id: number): Promise<BizRecordDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<BizRecordDto>>(
        `${API_BASE_URL}/${endpoint}/${id}`,
      ),
    ).then((response) => response.data);
  }

  create(
    endpoint: string,
    payload: Record<string, BizValue>,
  ): Promise<BizRecordDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<BizRecordDto>>(
        `${API_BASE_URL}/${endpoint}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  update(
    endpoint: string,
    id: number,
    payload: Record<string, BizValue>,
  ): Promise<BizRecordDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<BizRecordDto>>(
        `${API_BASE_URL}/${endpoint}/${id}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  saveMatrix(
    endpoint: string,
    payload: Record<string, unknown>,
  ): Promise<BizRecordDto[]> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<BizRecordDto[]>>(
        `${API_BASE_URL}/${endpoint}/matrix`,
        payload,
      ),
    ).then((response) => response.data);
  }

  finalizePeriod<T>(
    endpoint: string,
    payload: Record<string, unknown>,
  ): Promise<T> {
    const requestBody = {
      ...payload,
      moduleKey: endpoint,
    };

    return firstValueFrom(
      this.httpClient.post<ApiResponse<T>>(
        `${API_BASE_URL}/snapshot/finalize-module`,
        requestBody,
      ),
    ).then((response) => response.data);
  }

  delete(endpoint: string, id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/${endpoint}/${id}`,
      ),
    ).then(() => undefined);
  }
}
