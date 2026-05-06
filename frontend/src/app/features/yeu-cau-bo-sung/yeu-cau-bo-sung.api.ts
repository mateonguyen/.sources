import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface YeuCauBoSungDto {
  id: number;
  kyBaoCaoId: number;
  donViId: number;
  trangThai: number;
  lyDo: string;
  requestedBy: number;
  requestedAt: string;
  approvedBy?: number;
  approvedAt?: string;
  tuChoiLyDo?: string;
  hanBoSung?: string;
  completedAt?: string;
}

export interface CreateYeuCauBoSungRequest {
  kyBaoCaoId: number;
  donViId: number;
  lyDo: string;
  hanBoSung?: string;
}

export interface DuyetYeuCauRequest {
  hanBoSung?: string;
}

export interface TuChoiYeuCauRequest {
  tuChoiLyDo: string;
}

@Injectable({ providedIn: 'root' })
export class YeuCauBoSungApi {
  constructor(private readonly httpClient: HttpClient) {}

  getByKy(kyBaoCaoId: number): Promise<YeuCauBoSungDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<YeuCauBoSungDto[]>>(
        `${API_BASE_URL}/yeu-cau-bo-sung?kyBaoCaoId=${kyBaoCaoId}`,
      ),
    ).then((response) => response.data);
  }

  create(payload: CreateYeuCauBoSungRequest): Promise<YeuCauBoSungDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<YeuCauBoSungDto>>(
        `${API_BASE_URL}/yeu-cau-bo-sung`,
        payload,
      ),
    ).then((response) => response.data);
  }

  duyet(id: number, payload: DuyetYeuCauRequest): Promise<YeuCauBoSungDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<YeuCauBoSungDto>>(
        `${API_BASE_URL}/yeu-cau-bo-sung/${id}/duyet`,
        payload,
      ),
    ).then((response) => response.data);
  }

  tuChoi(id: number, payload: TuChoiYeuCauRequest): Promise<YeuCauBoSungDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<YeuCauBoSungDto>>(
        `${API_BASE_URL}/yeu-cau-bo-sung/${id}/tu-choi`,
        payload,
      ),
    ).then((response) => response.data);
  }
}
