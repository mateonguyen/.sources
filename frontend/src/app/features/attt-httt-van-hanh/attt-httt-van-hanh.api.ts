import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface AtttHtttVanHanhDto {
  id: number;
  donViId: number;
  htttId: number;
  loaiHaTang: string | null;
  chuQuan: string | null;
  donViVanHanh: string | null;
  capDoDeXuat: string | null;
  tinhTrangPheDuyet: string | null;
  quyetDinhPheDuyet: string | null;
  quyCheAttt: string | null;
  duKienNgayPheDuyet: string | null;
  daTrienKhaiPhuongAn: boolean;
  duKienNgayTrienKhai: string | null;
  kiemTraDanhGia: string | null;
  ghiChu: string | null;
}

export interface UpsertAtttHtttVanHanhRequest {
  donViId: number;
  htttId: number;
  loaiHaTang: string | null;
  chuQuan: string | null;
  donViVanHanh: string | null;
  capDoDeXuat: string | null;
  tinhTrangPheDuyet: string | null;
  quyetDinhPheDuyet: string | null;
  quyCheAttt: string | null;
  duKienNgayPheDuyet: string | null;
  daTrienKhaiPhuongAn: boolean;
  duKienNgayTrienKhai: string | null;
  kiemTraDanhGia: string | null;
  ghiChu: string | null;
}

@Injectable({ providedIn: 'root' })
export class AtttHtttVanHanhApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(donViId?: number): Promise<AtttHtttVanHanhDto[]> {
    let params = new HttpParams();
    if (donViId != null) {
      params = params.set('donViId', String(donViId));
    }
    return firstValueFrom(
      this.httpClient.get<ApiResponse<AtttHtttVanHanhDto[]>>(
        `${API_BASE_URL}/attt-httt-van-hanh`,
        { params },
      ),
    ).then((r) => r.data);
  }

  create(payload: UpsertAtttHtttVanHanhRequest): Promise<AtttHtttVanHanhDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<AtttHtttVanHanhDto>>(
        `${API_BASE_URL}/attt-httt-van-hanh`,
        payload,
      ),
    ).then((r) => r.data);
  }

  update(id: number, payload: UpsertAtttHtttVanHanhRequest): Promise<AtttHtttVanHanhDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<AtttHtttVanHanhDto>>(
        `${API_BASE_URL}/attt-httt-van-hanh/${id}`,
        payload,
      ),
    ).then((r) => r.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/attt-httt-van-hanh/${id}`,
      ),
    ).then(() => undefined);
  }
}
