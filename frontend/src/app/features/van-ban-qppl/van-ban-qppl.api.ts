import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';
import { FileMetadataDto } from '../files/files.api';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface VanBanQpplDto {
  id: number;
  donViId: number;
  kyBaoCaoCode: string | null;
  soHieu: string;
  tenVanBan: string | null;
  loaiVanBan: string | null;
  coQuanBanHanh: string | null;
  ngayBanHanh: string;
  ngayHieuLuc: string | null;
  linhVuc: string | null;
  trichYeu: string | null;
  tinhTrangTrienKhai: string | null;
  ghiChu: string | null;
  fileDinhKems: FileMetadataDto[];
}

export interface UpsertVanBanQpplRequest {
  donViId: number;
  soHieu: string;
  tenVanBan: string | null;
  loaiVanBan: string | null;
  coQuanBanHanh: string | null;
  ngayBanHanh: string;
  ngayHieuLuc: string | null;
  linhVuc: string | null;
  trichYeu: string | null;
  tinhTrangTrienKhai: string | null;
  ghiChu: string | null;
}

@Injectable({ providedIn: 'root' })
export class VanBanQpplApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/van-ban-qppl`;

  getAll(donViId?: number): Promise<VanBanQpplDto[]> {
    const params: Record<string, string> = {};
    if (donViId) params['donViId'] = String(donViId);
    return firstValueFrom(
      this.http.get<ApiResponse<VanBanQpplDto[]>>(this.base, { params }),
    ).then((r) => r.data);
  }

  getById(id: number): Promise<VanBanQpplDto> {
    return firstValueFrom(
      this.http.get<ApiResponse<VanBanQpplDto>>(`${this.base}/${id}`),
    ).then((r) => r.data);
  }

  create(request: UpsertVanBanQpplRequest): Promise<VanBanQpplDto> {
    return firstValueFrom(
      this.http.post<ApiResponse<VanBanQpplDto>>(this.base, request),
    ).then((r) => r.data);
  }

  update(id: number, request: UpsertVanBanQpplRequest): Promise<VanBanQpplDto> {
    return firstValueFrom(
      this.http.put<ApiResponse<VanBanQpplDto>>(`${this.base}/${id}`, request),
    ).then((r) => r.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.http.delete<ApiResponse<void>>(`${this.base}/${id}`),
    ).then(() => undefined);
  }
}
