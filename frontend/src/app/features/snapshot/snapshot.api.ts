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
  kyCode: string;
  donViId: number;
  tenDonVi: string;
  trangThai: number;
  phienBan: number;
  ghiChu?: string;
  submittedAt?: string;
  lockedAt?: string;
}

export interface ModuleStatusDto {
  moduleCode: string;
  recordCount: number;
  /** Số bản ghi do chính đơn vị nhập (TU_NHAP = recordCount). */
  ownRecordCount: number;
  /** Số bản ghi gộp từ đơn vị cấp dưới (chỉ khác 0 khi TONG_HOP). */
  childRecordCount: number;
}

export interface SubmitSnapshotContextDto {
  cheDoNhapLieu: string;
  isTongHop: boolean;
  totalChildren: number;
  confirmedChildren: number;
  hasUnconfirmedChildren: boolean;
  hasChildDataChangedAfterLastSubmit: boolean;
}

export interface SubmitCurrentSnapshotRequest {
  kyBaoCaoId: number;
  donViId: number;
  ghiChu?: string;
  forceSubmitWhenChildrenUnconfirmed?: boolean;
}

export interface SnapshotPdfResultDto {
  snapshotId: number;
  fileName: string;
  downloadUrl: string;
}

export interface SnapshotBreakdownUnitDto {
  donViId: number;
  tenDonVi: string;
  daXacNhan: boolean;
  moduleCounts: ModuleStatusDto[];
}

export interface SnapshotBreakdownDto {
  snapshotId: number;
  kyBaoCaoId: number;
  kyCode: string;
  donViId: number;
  tenDonVi: string;
  submittedAt?: string;
  children?: SnapshotBreakdownUnitDto[];
}

export interface SnapshotModuleCompareItemDto {
  moduleCode: string;
  fromCount: number;
  toCount: number;
  delta: number;
}

export interface SnapshotCompareDto {
  donViId: number;
  tenDonVi: string;
  fromKyBaoCaoId: number;
  fromKyCode: string;
  fromSnapshotId: number;
  toKyBaoCaoId: number;
  toKyCode: string;
  toSnapshotId: number;
  modules: SnapshotModuleCompareItemDto[];
}

@Injectable({ providedIn: 'root' })
export class SnapshotApi {
  constructor(private readonly httpClient: HttpClient) {}

  getByKy(kyBaoCaoId: number): Promise<SnapshotDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotDto[]>>(
        `${API_BASE_URL}/snapshot`,
        { params: { kyBaoCaoId } },
      ),
    ).then((response) => response.data ?? []);
  }

  getLatestByDonVi(kyBaoCaoId?: number): Promise<SnapshotDto[]> {
    const options = kyBaoCaoId != null ? { params: { kyBaoCaoId } } : {};
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotDto[]>>(
        `${API_BASE_URL}/snapshot/latest-by-don-vi`,
        options,
      ),
    ).then((response) => response.data ?? []);
  }

  compareTwoKy(
    donViId: number,
    fromKyBaoCaoId: number,
    toKyBaoCaoId: number,
  ): Promise<SnapshotCompareDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotCompareDto>>(
        `${API_BASE_URL}/snapshot/compare`,
        {
          params: {
            donViId,
            fromKyBaoCaoId,
            toKyBaoCaoId,
          },
        },
      ),
    ).then((response) => response.data);
  }

  getById(id: number): Promise<SnapshotDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotDto>>(
        `${API_BASE_URL}/snapshot/${id}`,
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

  cancel(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<null>>(
        `${API_BASE_URL}/snapshot/${id}`,
      ),
    ).then(() => undefined);
  }

  getModuleStatus(kyBaoCaoId: number): Promise<ModuleStatusDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<ModuleStatusDto[]>>(
        `${API_BASE_URL}/snapshot/module-status`,
        { params: { kyBaoCaoId } },
      ),
    ).then((response) => response.data ?? []);
  }

  getSubmitContext(kyBaoCaoId: number): Promise<SubmitSnapshotContextDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SubmitSnapshotContextDto>>(
        `${API_BASE_URL}/snapshot/submit-context`,
        { params: { kyBaoCaoId } },
      ),
    ).then((response) => response.data);
  }

  getBreakdown(id: number): Promise<SnapshotBreakdownDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotBreakdownDto>>(
        `${API_BASE_URL}/snapshot/${id}/breakdown`,
      ),
    ).then((response) => response.data);
  }

  getPdf(id: number): Promise<SnapshotPdfResultDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotPdfResultDto>>(
        `${API_BASE_URL}/snapshot/${id}/pdf`,
      ),
    ).then((response) => response.data);
  }

  /** Xuất biểu mẫu báo cáo (mẫu H05) từ dữ liệu đã chốt. */
  getExport(id: number, format: 'xlsx' | 'pdf'): Promise<SnapshotExportResultDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<SnapshotExportResultDto>>(
        `${API_BASE_URL}/snapshot/${id}/export`,
        { params: { format } },
      ),
    ).then((response) => response.data);
  }
}

export interface SnapshotExportResultDto {
  snapshotId: number;
  format: string;
  fileName: string;
  downloadUrl: string;
}
