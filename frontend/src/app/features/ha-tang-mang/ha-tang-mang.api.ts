import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface HaTangMangDto {
  id: number;
  donViId: number;
  soDonViTrucThuoc: number;
  soDaKetNoiBcanet: number;
  soDuongTruyenVnpt: number;
  soDuongTruyenKhac: number;
  soKetNoiInternet: number;
  ghiChu?: string | null;
}

export interface UpsertHaTangMangRequest {
  donViId: number;
  soDonViTrucThuoc: number;
  soDaKetNoiBcanet: number;
  soDuongTruyenVnpt: number;
  soDuongTruyenKhac: number;
  soKetNoiInternet: number;
  ghiChu?: string | null;
}

export interface SaveHaTangMangMatrixRequest {
  donViId: number;
  items: UpsertHaTangMangRequest[];
}

@Injectable({ providedIn: 'root' })
export class HaTangMangApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(donViId: number): Promise<HaTangMangDto[]> {
    const params = new HttpParams().set('donViId', String(donViId));
    return firstValueFrom(
      this.httpClient.get<ApiResponse<HaTangMangDto[]>>(
        `${API_BASE_URL}/ha-tang-mang`,
        { params },
      ),
    ).then((response) => response.data);
  }

  saveMatrix(payload: SaveHaTangMangMatrixRequest): Promise<HaTangMangDto[]> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<HaTangMangDto[]>>(
        `${API_BASE_URL}/ha-tang-mang/matrix`,
        payload,
      ),
    ).then((response) => response.data);
  }
}
