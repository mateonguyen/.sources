import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface KyBaoCaoDto {
  id: number;
  kyCode: string;
  mauBaoCaoId?: number;
  nam: number;
  quy?: number;
  thang?: number;
  trangThai: number;
  ngayBatDau?: string;
  ngayKetThuc?: string;
  ghiChu?: string;
}

export interface KyBaoCaoTienDoDonViDto {
  donViId: number;
  tenDonVi: string;
  trangThai: number;
  kyTrangThai?: number;
  trangThaiDonVi?: number;
  snapshotId?: number;
  snapshotPhienBan?: number;
  snapshotTrangThai?: number;
  submittedAt?: string;
  lockedAt?: string;
  ngayMoLai?: string;
  ngayXacNhan?: string;
}

export interface KyBaoCaoTienDoDto {
  kyBaoCaoId: number;
  kyCode: string;
  tongDonVi: number;
  soDonViChuaNhap: number;
  soDonViDangNhap: number;
  soDonViDaXacNhan: number;
  soDonViDangBoSung: number;
  soDonViDaNop: number;
  donVis: KyBaoCaoTienDoDonViDto[];
  danhSachDonVi: KyBaoCaoTienDoDonViDto[];
}

export interface CreateKyBaoCaoRequest {
  mauBaoCaoId: number;
  nam: number;
  quy?: number;
  thang?: number;
  ngayBatDau?: string;
  ngayKetThuc?: string;
  ghiChu?: string;
}

export interface UpdateKyBaoCaoStatusRequest {
  trangThai: number;
  ghiChu?: string;
}

@Injectable({ providedIn: 'root' })
export class KyBaoCaoApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(): Promise<KyBaoCaoDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<KyBaoCaoDto[]>>(
        `${API_BASE_URL}/ky-bao-cao`,
      ),
    ).then((response) => response.data);
  }

  getCurrent(): Promise<KyBaoCaoDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<KyBaoCaoDto>>(
        `${API_BASE_URL}/ky-bao-cao/current`,
      ),
    ).then((response) => response.data);
  }

  getTienDo(id: number): Promise<KyBaoCaoTienDoDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<KyBaoCaoTienDoDto>>(
        `${API_BASE_URL}/ky-bao-cao/${id}/tien-do`,
      ),
    ).then((response) => {
      const data = response.data as KyBaoCaoTienDoDto & {
        donVis?: KyBaoCaoTienDoDonViDto[];
        danhSachDonVi?: KyBaoCaoTienDoDonViDto[];
      };

      const rows = (data.danhSachDonVi ?? data.donVis ?? []).map((row) => ({
        ...row,
        trangThai: row.trangThai ?? row.trangThaiDonVi ?? row.kyTrangThai ?? 1,
      }));

      return {
        ...data,
        donVis: rows,
        danhSachDonVi: rows,
      };
    });
  }

  create(payload: CreateKyBaoCaoRequest): Promise<KyBaoCaoDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<KyBaoCaoDto>>(
        `${API_BASE_URL}/ky-bao-cao`,
        payload,
      ),
    ).then((response) => response.data);
  }

  updateStatus(
    id: number,
    payload: UpdateKyBaoCaoStatusRequest,
  ): Promise<KyBaoCaoDto> {
    return firstValueFrom(
      this.httpClient.patch<ApiResponse<KyBaoCaoDto>>(
        `${API_BASE_URL}/ky-bao-cao/${id}/status`,
        payload,
      ),
    ).then((response) => response.data);
  }
}
