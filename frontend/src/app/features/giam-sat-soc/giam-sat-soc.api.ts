import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface GiamSatSocDto {
  id: number;
  donViId: number;
  loaiMang: string;
  lopGiamSat: string;
  coHeThong: boolean;
  thucTrang?: string | null;
  namThanhLap?: number | null;
  soNhanSu?: number | null;
  congCuSuDung?: string | null;
  soCanhBaoThang?: number | null;
  ghiChu?: string | null;
}

export interface GiamSatSocQuery {
  donViId?: number | null;
}

export interface UpsertGiamSatSocRequest {
  donViId: number;
  loaiMang: string;
  lopGiamSat: string;
  coHeThong: boolean;
  thucTrang?: string | null;
  namThanhLap?: number | null;
  soNhanSu?: number | null;
  congCuSuDung?: string | null;
  soCanhBaoThang?: number | null;
  ghiChu?: string | null;
}

export interface SaveGiamSatSocMatrixRequest {
  donViId: number;
  items: UpsertGiamSatSocRequest[];
}

@Injectable({ providedIn: 'root' })
export class GiamSatSocApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(query: GiamSatSocQuery): Promise<GiamSatSocDto[]> {
    let params = new HttpParams();
    if (query.donViId != null) {
      params = params.set('donViId', String(query.donViId));
    }

    return firstValueFrom(
      this.httpClient.get<ApiResponse<GiamSatSocDto[]>>(
        `${API_BASE_URL}/giam-sat-soc`,
        { params },
      ),
    ).then((response) => response.data);
  }

  saveMatrix(payload: SaveGiamSatSocMatrixRequest): Promise<GiamSatSocDto[]> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<GiamSatSocDto[]>>(
        `${API_BASE_URL}/giam-sat-soc/matrix`,
        payload,
      ),
    ).then((response) => response.data);
  }
}
