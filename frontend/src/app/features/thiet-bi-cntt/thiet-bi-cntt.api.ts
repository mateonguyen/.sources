import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface RefLoaiThietBiDto {
  id: number;
  parentId: number | null;
  maLoai: string;
  tenLoai: string;
  cap: number;
  laTongHop: boolean;
  sortOrder: number;
  children: RefLoaiThietBiDto[];
}

export interface HeThongThongTinOptionDto {
  id: number;
  donViId: number;
  loaiPhanMem: string;
  tenPhanMem: string;
  maPhanMem?: string | null;
  donViPhatTrien?: string | null;
  donViQuanLy?: string | null;
  namTrienKhai?: number | null;
  phamViHoatDong?: string | null;
  ungDungCnMoi: boolean;
  khaNangTichHop: boolean;
  daCongNhanSangKien: boolean;
  ghiChu?: string | null;
}

export interface ThietBiCnttDto {
  id: number;
  donViId: number;
  loaiThietBiId: number;
  tenThietBi?: string | null;
  hangSanXuat?: string | null;
  model?: string | null;
  cauHinh?: string | null;
  heDieuHanh?: string | null;
  donViSuDung?: string | null;
  soLuongTong: number;
  soLuongHienDung: number;
  soLuongHong: number;
  tinhTrang?: string | null;
  ghiChu?: string | null;
  ungDungIds: number[];
}

export interface UpsertThietBiCnttRequest {
  donViId: number;
  loaiThietBiId: number;
  tenThietBi?: string | null;
  hangSanXuat?: string | null;
  model?: string | null;
  cauHinh?: string | null;
  heDieuHanh?: string | null;
  donViSuDung?: string | null;
  soLuongTong: number;
  soLuongHienDung: number;
  soLuongHong: number;
  tinhTrang?: string | null;
  ghiChu?: string | null;
  ungDungIds: number[];
}

@Injectable({ providedIn: 'root' })
export class ThietBiCnttApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(): Promise<ThietBiCnttDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<ThietBiCnttDto[]>>(
        `${API_BASE_URL}/thiet-bi-cntt`,
      ),
    ).then((response) => response.data);
  }

  getById(id: number): Promise<ThietBiCnttDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<ThietBiCnttDto>>(
        `${API_BASE_URL}/thiet-bi-cntt/${id}`,
      ),
    ).then((response) => response.data);
  }

  create(payload: UpsertThietBiCnttRequest): Promise<ThietBiCnttDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<ThietBiCnttDto>>(
        `${API_BASE_URL}/thiet-bi-cntt`,
        payload,
      ),
    ).then((response) => response.data);
  }

  update(
    id: number,
    payload: UpsertThietBiCnttRequest,
  ): Promise<ThietBiCnttDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<ThietBiCnttDto>>(
        `${API_BASE_URL}/thiet-bi-cntt/${id}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/thiet-bi-cntt/${id}`,
      ),
    ).then(() => undefined);
  }

  getLoaiThietBiTree(): Promise<RefLoaiThietBiDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<RefLoaiThietBiDto[]>>(
        `${API_BASE_URL}/ref-loai-thiet-bi/tree`,
      ),
    ).then((response) => response.data);
  }

  getHeThongThongTin(): Promise<HeThongThongTinOptionDto[]> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<HeThongThongTinOptionDto[]>>(
        `${API_BASE_URL}/he-thong-thong-tin`,
      ),
    ).then((response) => response.data);
  }
}
