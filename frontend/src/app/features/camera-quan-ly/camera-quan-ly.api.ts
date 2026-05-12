import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

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
  private readonly endpoint = '/api/v1/camera-quan-ly';

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
      this.http.get<CameraQuanLyDto[]>(this.endpoint, { params }),
    );
  }

  async getById(id: number): Promise<CameraQuanLyDto> {
    return firstValueFrom(
      this.http.get<CameraQuanLyDto>(`${this.endpoint}/${id}`),
    );
  }

  async create(request: UpsertCameraQuanLyRequest): Promise<CameraQuanLyDto> {
    return firstValueFrom(
      this.http.post<CameraQuanLyDto>(this.endpoint, request),
    );
  }

  async update(
    id: number,
    request: UpsertCameraQuanLyRequest,
  ): Promise<CameraQuanLyDto> {
    return firstValueFrom(
      this.http.put<CameraQuanLyDto>(`${this.endpoint}/${id}`, request),
    );
  }

  async delete(id: number): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.endpoint}/${id}`));
  }
}
