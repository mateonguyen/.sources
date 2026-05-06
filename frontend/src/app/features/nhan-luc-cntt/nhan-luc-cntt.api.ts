import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';
import {
  NhanLucCnttItem,
  NhanLucCnttSearchParams,
  NhanLucCnttUpsertPayload,
  PagedResult,
} from './nhan-luc-cntt.models';
import { NhanLucCnttMockRepository } from './nhan-luc-cntt.mock';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

@Injectable({ providedIn: 'root' })
export class NhanLucCnttApi {
  private readonly mockRepository = new NhanLucCnttMockRepository();
  private preferMock = false;

  constructor(private readonly httpClient: HttpClient) {}

  search(
    params: NhanLucCnttSearchParams,
  ): Promise<PagedResult<NhanLucCnttItem>> {
    return this.withFallback(
      async () => {
        const queryParams = this.buildSearchParams(params);

        const response = await firstValueFrom(
          this.httpClient.get<ApiResponse<PagedResult<NhanLucCnttItem>>>(
            `${API_BASE_URL}/nhan-luc-cntt`,
            { params: queryParams },
          ),
        );

        return response.data;
      },
      () => this.mockRepository.search(params),
    );
  }

  create(payload: NhanLucCnttUpsertPayload): Promise<NhanLucCnttItem> {
    return this.withFallback(
      async () => {
        const response = await firstValueFrom(
          this.httpClient.post<ApiResponse<NhanLucCnttItem>>(
            `${API_BASE_URL}/nhan-luc-cntt`,
            payload,
          ),
        );

        return response.data;
      },
      () => this.mockRepository.create(payload),
    );
  }

  getById(id: number): Promise<NhanLucCnttItem> {
    return this.withFallback(
      async () => {
        const response = await firstValueFrom(
          this.httpClient.get<ApiResponse<NhanLucCnttItem>>(
            `${API_BASE_URL}/nhan-luc-cntt/${id}`,
          ),
        );

        return response.data;
      },
      () => this.mockRepository.getById(id),
    );
  }

  update(
    id: number,
    payload: NhanLucCnttUpsertPayload,
  ): Promise<NhanLucCnttItem> {
    return this.withFallback(
      async () => {
        const response = await firstValueFrom(
          this.httpClient.put<ApiResponse<NhanLucCnttItem>>(
            `${API_BASE_URL}/nhan-luc-cntt/${id}`,
            payload,
          ),
        );

        return response.data;
      },
      () => this.mockRepository.update(id, payload),
    );
  }

  delete(id: number): Promise<void> {
    return this.withFallback(
      async () => {
        await firstValueFrom(
          this.httpClient.delete<ApiResponse<unknown>>(
            `${API_BASE_URL}/nhan-luc-cntt/${id}`,
          ),
        );

        return undefined;
      },
      () => this.mockRepository.delete(id),
    );
  }

  private async withFallback<T>(
    primary: () => Promise<T>,
    fallback: () => Promise<T>,
  ): Promise<T> {
    if (this.preferMock) {
      return fallback();
    }

    try {
      return await primary();
    } catch {
      this.preferMock = true;
      return fallback();
    }
  }

  private buildSearchParams(params: NhanLucCnttSearchParams): HttpParams {
    let httpParams = new HttpParams()
      .set('page', String(params.page))
      .set('pageSize', String(params.pageSize));

    if (params.tuKhoa?.trim()) {
      httpParams = httpParams.set('tuKhoa', params.tuKhoa.trim());
    }

    if (params.namBaoCao) {
      httpParams = httpParams.set('namBaoCao', String(params.namBaoCao));
    }

    if (params.donViCongTacId) {
      httpParams = httpParams.set(
        'donViCongTacId',
        String(params.donViCongTacId),
      );
    }

    if (params.gioiTinh) {
      httpParams = httpParams.set('gioiTinh', params.gioiTinh);
    }

    if (params.capBac) {
      httpParams = httpParams.set('capBac', params.capBac);
    }

    if (params.loaiNhanLuc) {
      httpParams = httpParams.set('loaiNhanLuc', params.loaiNhanLuc);
    }

    if (params.trinhDoCntt) {
      httpParams = httpParams.set('trinhDoCntt', params.trinhDoCntt);
    }

    return httpParams;
  }
}
