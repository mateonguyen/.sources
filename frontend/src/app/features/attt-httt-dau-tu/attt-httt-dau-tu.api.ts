import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface AtttHtttDauTuDto {
  id: number;
  donViId: number;
  kyBaoCaoCode: string | null;
  htttId: number;
  chuQuan: string | null;
  donViVanHanh: string | null;
  capDoDeXuat: string | null;
  ngayPheDuyetHsdxcd: string | null;
  quyetDinhPheDuyet: string | null;
  daLongGhepThuyetMinh: boolean;
  ghiChu: string | null;
}

export interface UpsertAtttHtttDauTuRequest {
  donViId: number;
  htttId: number;
  chuQuan: string | null;
  donViVanHanh: string | null;
  capDoDeXuat: string | null;
  ngayPheDuyetHsdxcd: string | null;
  quyetDinhPheDuyet: string | null;
  daLongGhepThuyetMinh: boolean;
  ghiChu: string | null;
}

@Injectable({ providedIn: 'root' })
export class AtttHtttDauTuApi {
  constructor(private readonly httpClient: HttpClient) {}

  getAll(options?: {
    donViId?: number;
    kyCode?: string;
  }): Promise<AtttHtttDauTuDto[]> {
    let params = new HttpParams();
    if (options?.donViId != null) {
      params = params.set('donViId', String(options.donViId));
    }
    if (options?.kyCode) {
      params = params.set('kyCode', options.kyCode);
    }

    return firstValueFrom(
      this.httpClient.get<ApiResponse<AtttHtttDauTuDto[]>>(
        `${API_BASE_URL}/attt-httt-dau-tu`,
        { params },
      ),
    ).then((response) => response.data);
  }

  getById(id: number): Promise<AtttHtttDauTuDto> {
    return firstValueFrom(
      this.httpClient.get<ApiResponse<AtttHtttDauTuDto>>(
        `${API_BASE_URL}/attt-httt-dau-tu/${id}`,
      ),
    ).then((response) => response.data);
  }

  create(payload: UpsertAtttHtttDauTuRequest): Promise<AtttHtttDauTuDto> {
    return firstValueFrom(
      this.httpClient.post<ApiResponse<AtttHtttDauTuDto>>(
        `${API_BASE_URL}/attt-httt-dau-tu`,
        payload,
      ),
    ).then((response) => response.data);
  }

  update(
    id: number,
    payload: UpsertAtttHtttDauTuRequest,
  ): Promise<AtttHtttDauTuDto> {
    return firstValueFrom(
      this.httpClient.put<ApiResponse<AtttHtttDauTuDto>>(
        `${API_BASE_URL}/attt-httt-dau-tu/${id}`,
        payload,
      ),
    ).then((response) => response.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.httpClient.delete<ApiResponse<unknown>>(
        `${API_BASE_URL}/attt-httt-dau-tu/${id}`,
      ),
    ).then(() => undefined);
  }
}
