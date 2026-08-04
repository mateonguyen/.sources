import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface CameraQuanLyDto {
  id: number;
  donViId: number;
  kyBaoCaoCode: string;
  nhomCamera: string | null;
  tenDonViDiaChi: string;
  buongGiamTrangBiSl: number;
  buongGiamTrangBiTs: number;
  nhuCauDauTu: number;
  baoTri: number;
  suaChua: number;
  soLanViPham: number;
  ketNoiChiaSe: string | null;
  hoSoCapDoAttt: number;
  cbChuyenTrach: number;
  cbKiemNhiem: number;
  cbDiaPhuong: number;
  daoTaoBo: number;
  daoTaoNhuCau: number;
  ghiChu: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpsertCameraQuanLyRequest {
  donViId: number;
  nhomCamera: string | null;
  tenDonViDiaChi: string;
  buongGiamTrangBiSl: number;
  buongGiamTrangBiTs: number;
  nhuCauDauTu: number;
  baoTri: number;
  suaChua: number;
  soLanViPham: number;
  ketNoiChiaSe: string | null;
  hoSoCapDoAttt: number;
  cbChuyenTrach: number;
  cbKiemNhiem: number;
  cbDiaPhuong: number;
  daoTaoBo: number;
  daoTaoNhuCau: number;
  ghiChu: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class CameraQuanLyApi {
  private readonly endpoint = `${API_BASE_URL}/camera-quan-ly`;

  constructor(private readonly http: HttpClient) {}

  async getAll(filters?: {
    donViId?: number;
    nhomCamera?: string;
  }): Promise<CameraQuanLyDto[]> {
    let params = new HttpParams();
    if (filters?.donViId) {
      params = params.set('donViId', filters.donViId.toString());
    }
    if (filters?.nhomCamera) {
      params = params.set('nhomCamera', filters.nhomCamera);
    }

    return firstValueFrom(
      this.http.get<ApiResponse<CameraQuanLyDto[]>>(this.endpoint, { params }),
    ).then((response) => response.data);
  }

  async getById(id: number): Promise<CameraQuanLyDto> {
    return firstValueFrom(
      this.http.get<ApiResponse<CameraQuanLyDto>>(`${this.endpoint}/${id}`),
    ).then((response) => response.data);
  }

  async create(request: UpsertCameraQuanLyRequest): Promise<CameraQuanLyDto> {
    return firstValueFrom(
      this.http.post<ApiResponse<CameraQuanLyDto>>(this.endpoint, request),
    ).then((response) => response.data);
  }

  async update(
    id: number,
    request: UpsertCameraQuanLyRequest,
  ): Promise<CameraQuanLyDto> {
    return firstValueFrom(
      this.http.put<ApiResponse<CameraQuanLyDto>>(
        `${this.endpoint}/${id}`,
        request,
      ),
    ).then((response) => response.data);
  }

  async delete(id: number): Promise<void> {
    return firstValueFrom(
      this.http.delete<ApiResponse<null>>(`${this.endpoint}/${id}`),
    ).then(() => undefined);
  }
}
