import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface SnapshotDto {
  id: number;
  kyBaoCaoId: number;
  donViId: number;
  trangThai: number;
  phienBan: number;
  snapshotJson: string;
  summaryJson?: string;
  ghiChu?: string;
  submittedAt?: string;
  lockedAt?: string;
}

export interface SnapshotPdfResultDto {
  snapshotId: number;
  fileName: string;
  downloadUrl: string;
}

export interface SubmitCurrentSnapshotRequest {
  kyBaoCaoId: number;
  donViId: number;
  summaryJson?: string;
  ghiChu?: string;
}

@Injectable({ providedIn: 'root' })
export class SnapshotApi {
  constructor(private readonly httpClient: HttpClient) {}

  getByKy(kyBaoCaoId: number): Promise<SnapshotDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotDto[]>>(
        `${API_BASE_URL}/snapshot?kyBaoCaoId=${kyBaoCaoId}`,
      ),
    ).then((response) => response.data);
  }

  buildSnapshotJson(kyId: number, donViId: number): Promise<string> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<string>>(
        `${API_BASE_URL}/snapshot/build?kyId=${kyId}&donViId=${donViId}`,
      ),
    ).then((response) => response.data);
  }

  createDraft(
    kyBaoCaoId: number,
    donViId: number,
    snapshotJson: string,
    summaryJson?: string,
  ): Promise<SnapshotDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<SnapshotDto>>(
        `${API_BASE_URL}/snapshot/create-draft`,
        { kyBaoCaoId, donViId, snapshotJson, summaryJson },
      ),
    ).then((response) => response.data);
  }

  submit(snapshotId: number, ghiChu?: string): Promise<SnapshotDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<SnapshotDto>>(
        `${API_BASE_URL}/snapshot/${snapshotId}/submit`,
        { ghiChu },
      ),
    ).then((response) => response.data);
  }

  submitCurrent(payload: SubmitCurrentSnapshotRequest): Promise<SnapshotDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<SnapshotDto>>(
        `${API_BASE_URL}/snapshot/submit-current`,
        payload,
      ),
    ).then((response) => response.data);
  }

  cancel(snapshotId: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/snapshot/${snapshotId}`,
      ),
    ).then(() => undefined);
  }

  getById(snapshotId: number): Promise<SnapshotDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotDto>>(
        `${API_BASE_URL}/snapshot/${snapshotId}`,
      ),
    ).then((response) => response.data);
  }

  updateDraft(
    snapshotId: number,
    snapshotJson: string,
    summaryJson?: string,
    ghiChu?: string,
  ): Promise<SnapshotDto> {
    return firstValueFrom(
      this.httpClient.patch<ApiResponse<SnapshotDto>>(
        `${API_BASE_URL}/snapshot/${snapshotId}`,
        {
          snapshotJson,
          summaryJson,
          ghiChu,
        },
      ),
    ).then((response) => response.data);
  }

  getPdf(snapshotId: number): Promise<SnapshotPdfResultDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotPdfResultDto>>(
        `${API_BASE_URL}/snapshot/${snapshotId}/pdf`,
      ),
    ).then((response) => response.data);
  }
}
