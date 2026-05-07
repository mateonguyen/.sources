import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface GiamSatNocDto {
  id: number;
  donViId: number;
  lopGiamSat: string;
  coNoc: boolean;
  thucTrang?: string | null;
  tongSoDoiTuong: number;
  soDaGiamSat: number;
  ghiChu?: string | null;
}

export interface GiamSatNocQuery {
  donViId?: number | null;
}

export interface UpsertGiamSatNocRequest {
  donViId: number;
  lopGiamSat: string;
  coNoc: boolean;
  thucTrang?: string | null;
  tongSoDoiTuong: number;
  soDaGiamSat: number;
  ghiChu?: string | null;
}

export interface SaveGiamSatNocMatrixRequest {
  donViId: number;
  items: UpsertGiamSatNocRequest[];
}

@Injectable({ providedIn: 'root' })
export class GiamSatNocApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(query: GiamSatNocQuery): Promise<GiamSatNocDto[]> {
    let params = new HttpParams();
    if (query.donViId != null) {
      params = params.set('donViId', String(query.donViId));
    }
    return firstValueFrom(
      this.httpClient.get<ApiResponse<GiamSatNocDto[]>>(
        `${API_BASE_URL}/giam-sat-noc`,
        { params },
      ),
    ).then((response) => response.data);
  }

  saveMatrix(payload: SaveGiamSatNocMatrixRequest): Promise<GiamSatNocDto[]> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<GiamSatNocDto[]>>(
        `${API_BASE_URL}/giam-sat-noc/matrix`,
        payload,
      ),
    ).then((response) => response.data);
  }
}
