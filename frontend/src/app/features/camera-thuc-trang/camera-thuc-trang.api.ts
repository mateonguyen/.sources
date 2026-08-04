import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface CameraThucTrangDto {
  id: number;
  donViId: number;
  kyBaoCaoCode: string | null;
  nhomCamera: string | null;
  tenHeThong: string;
  cauHinhIp: number;
  cauHinhAnalog: number;
  thucTrangIp: number;
  thucTrangAnalog: number;
  chuDauTu: string | null;
  namDauTu: number | null;
  duongTruyen: string | null;
  phanMem: string | null;
  luuTru: string | null;
  ghiChu: string | null;
}

export interface UpsertCameraThucTrangRequest {
  donViId: number;
  nhomCamera: string | null;
  tenHeThong: string;
  cauHinhIp: number;
  cauHinhAnalog: number;
  thucTrangIp: number;
  thucTrangAnalog: number;
  chuDauTu: string | null;
  namDauTu: number | null;
  duongTruyen: string | null;
  phanMem: string | null;
  luuTru: string | null;
  ghiChu: string | null;
}

@Injectable({
  providedIn: 'root',
})
export class CameraThucTrangApi {
  private readonly endpoint = `${API_BASE_URL}/camera-thuc-trang`;

  constructor(private readonly http: HttpClient) {}

  async getAll(filters?: {
    donViId?: number;
    nhomCamera?: string;
    kyBaoCaoCode?: string;
  }): Promise<CameraThucTrangDto[]> {
    let params = new HttpParams();

    if (filters?.donViId) {
      params = params.set('donViId', filters.donViId.toString());
    }

    if (filters?.nhomCamera) {
      params = params.set('nhomCamera', filters.nhomCamera);
    }

    if (filters?.kyBaoCaoCode) {
      params = params.set('kyBaoCaoCode', filters.kyBaoCaoCode);
    }

    return firstValueFrom(
      this.http.get<ApiResponse<CameraThucTrangDto[]>>(this.endpoint, {
        params,
      }),
    ).then((response) => response.data);
  }

  async getById(id: number): Promise<CameraThucTrangDto> {
    return firstValueFrom(
      this.http.get<ApiResponse<CameraThucTrangDto>>(`${this.endpoint}/${id}`),
    ).then((response) => response.data);
  }

  async create(
    request: UpsertCameraThucTrangRequest,
  ): Promise<CameraThucTrangDto> {
    return firstValueFrom(
      this.http.post<ApiResponse<CameraThucTrangDto>>(this.endpoint, request),
    ).then((response) => response.data);
  }

  async update(
    id: number,
    request: UpsertCameraThucTrangRequest,
  ): Promise<CameraThucTrangDto> {
    return firstValueFrom(
      this.http.put<ApiResponse<CameraThucTrangDto>>(
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
