import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export type LoaiPhanMemCode =
  | 'DUNG_CHUNG'
  | 'TU_PHAT_TRIEN'
  | 'CHUA_PHAN_LOAI';

export interface HeThongThongTinDto {
  id: number;
  donViId: number;
  loaiPhanMem: LoaiPhanMemCode;
  tenPhanMem: string;
  donViPhatTrien?: string | null;
  donViQuanLy?: string | null;
  namTrienKhai?: number | null;
  phamViHoatDong?: string | null;
  phamViHoatDongKyThuat?: string | null;
  ungDungCnMoi?: string | null;
  khaNangTichHop?: string | null;
  daCongNhanSangKien: boolean;
  ghiChu?: string | null;
}

export interface UpsertHeThongThongTinRequest {
  donViId: number;
  loaiPhanMem: Exclude<LoaiPhanMemCode, 'CHUA_PHAN_LOAI'>;
  tenPhanMem: string;
  donViPhatTrien?: string | null;
  donViQuanLy?: string | null;
  namTrienKhai?: number | null;
  phamViHoatDong?: string | null;
  phamViHoatDongKyThuat?: string | null;
  ungDungCnMoi?: string | null;
  khaNangTichHop?: string | null;
  daCongNhanSangKien: boolean;
  ghiChu?: string | null;
}

export interface HtttTieuChuanDto {
  id: number;
  donViId: number;
  tenHeThong: string;
  dvt?: string | null;
  soH05: number;
  soTinh: number;
  soXa: number;
  soDvTrucThuocBo: number;
  ghiChu?: string | null;
}

export interface UpsertHtttTieuChuanRequest {
  donViId: number;
  tenHeThong: string;
  dvt?: string | null;
  soH05: number;
  soTinh: number;
  soXa: number;
  soDvTrucThuocBo: number;
  ghiChu?: string | null;
}

@Injectable({ providedIn: 'root' })
export class HeThongThongTinApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(): Promise<HeThongThongTinDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<HeThongThongTinDto[]>>(
        `${API_BASE_URL}/he-thong-thong-tin`,
      ),
    ).then((response) => response.data);
  }

  create(payload: UpsertHeThongThongTinRequest): Promise<HeThongThongTinDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<HeThongThongTinDto>>(
        `${API_BASE_URL}/he-thong-thong-tin`,
        payload,
      ),
    ).then((response) => response.data);
  }

  update(
    id: number,
    payload: UpsertHeThongThongTinRequest,
  ): Promise<HeThongThongTinDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<HeThongThongTinDto>>(
        `${API_BASE_URL}/he-thong-thong-tin/${id}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/he-thong-thong-tin/${id}`,
      ),
    ).then(() => undefined);
  }

  getAllTieuChuan(): Promise<HtttTieuChuanDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<HtttTieuChuanDto[]>>(
        `${API_BASE_URL}/httt-tieu-chuan`,
      ),
    ).then((response) => response.data);
  }

  createTieuChuan(
    payload: UpsertHtttTieuChuanRequest,
  ): Promise<HtttTieuChuanDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<HtttTieuChuanDto>>(
        `${API_BASE_URL}/httt-tieu-chuan`,
        payload,
      ),
    ).then((response) => response.data);
  }

  updateTieuChuan(
    id: number,
    payload: UpsertHtttTieuChuanRequest,
  ): Promise<HtttTieuChuanDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<HtttTieuChuanDto>>(
        `${API_BASE_URL}/httt-tieu-chuan/${id}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  deleteTieuChuan(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/httt-tieu-chuan/${id}`,
      ),
    ).then(() => undefined);
  }
}
