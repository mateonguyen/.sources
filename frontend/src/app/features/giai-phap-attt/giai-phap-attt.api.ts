import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface GiaiPhapAtttDto {
  id: number;
  donViId: number;
  tenGiaiPhap: string;
  mayTinhBcanetSl: number;
  mayTinhBcanetTs: number;
  mayTinhInternetSl: number;
  mayTinhInternetTs: number;
  mayTinhLocalSl: number;
  mayTinhLocalTs: number;
  mayChuBcanetSl: number;
  mayChuBcanetTs: number;
  mayChuInternetSl: number;
  mayChuInternetTs: number;
  mayChuLocalSl: number;
  mayChuLocalTs: number;
  ghiChu?: string | null;
}

export interface GiaiPhapAtttQuery {
  donViId?: number | null;
}

export interface UpsertGiaiPhapAtttRequest {
  donViId: number;
  tenGiaiPhap: string;
  mayTinhBcanetSl: number;
  mayTinhBcanetTs: number;
  mayTinhInternetSl: number;
  mayTinhInternetTs: number;
  mayTinhLocalSl: number;
  mayTinhLocalTs: number;
  mayChuBcanetSl: number;
  mayChuBcanetTs: number;
  mayChuInternetSl: number;
  mayChuInternetTs: number;
  mayChuLocalSl: number;
  mayChuLocalTs: number;
  ghiChu?: string | null;
}

export interface SaveGiaiPhapAtttMatrixRequest {
  donViId: number;
  items: UpsertGiaiPhapAtttRequest[];
}

@Injectable({ providedIn: 'root' })
export class GiaiPhapAtttApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(query: GiaiPhapAtttQuery): Promise<GiaiPhapAtttDto[]> {
    let params = new HttpParams();
    if (query.donViId != null) {
      params = params.set('donViId', String(query.donViId));
    }

    return firstValueFrom(
      this.httpClient.get<ApiResponse<GiaiPhapAtttDto[]>>(
        `${API_BASE_URL}/giai-phap-attt`,
        { params },
      ),
    ).then((response) => response.data);
  }

  saveMatrix(
    payload: SaveGiaiPhapAtttMatrixRequest,
  ): Promise<GiaiPhapAtttDto[]> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<GiaiPhapAtttDto[]>>(
        `${API_BASE_URL}/giai-phap-attt/matrix`,
        payload,
      ),
    ).then((response) => response.data);
  }
}
