import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data: T;
}

export interface DuAnCnttDto {
  id?: number;
  donViId: number;
  tenDuAn: string;
  donViChuTri?: string;
  namTrienKhai?: number;
  namDuaVaoSuDung?: number;
  tongKinhPhi?: number;
  nguonVon?: string;
  ghiChu?: string;
}

export interface UpsertDuAnCnttRequest {
  donViId: number;
  tenDuAn: string;
  donViChuTri?: string;
  namTrienKhai?: number;
  namDuaVaoSuDung?: number;
  tongKinhPhi?: number;
  nguonVon?: string;
  ghiChu?: string;
}

@Injectable({
  providedIn: 'root',
})
export class DuAnCnttApi {
  private readonly baseUrl = `${API_BASE_URL}/du-an-cntt`;

  constructor(private http: HttpClient) {}

  getAll(params?: Record<string, any>): Promise<DuAnCnttDto[]> {
    let httpParams = new HttpParams();
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (value !== null && value !== undefined) {
          httpParams = httpParams.set(key, String(value));
        }
      });
    }
    return firstValueFrom(
      this.http.get<ApiResponse<DuAnCnttDto[]>>(this.baseUrl, { params: httpParams }),
    ).then((r) => r.data ?? []);
  }

  getById(id: number): Promise<DuAnCnttDto> {
    return firstValueFrom(
      this.http.get<ApiResponse<DuAnCnttDto>>(`${this.baseUrl}/${id}`),
    ).then((r) => r.data);
  }

  create(payload: UpsertDuAnCnttRequest): Promise<DuAnCnttDto> {
    return firstValueFrom(
      this.http.post<ApiResponse<DuAnCnttDto>>(this.baseUrl, payload),
    ).then((r) => r.data);
  }

  update(id: number, payload: UpsertDuAnCnttRequest): Promise<DuAnCnttDto> {
    return firstValueFrom(
      this.http.put<ApiResponse<DuAnCnttDto>>(`${this.baseUrl}/${id}`, payload),
    ).then((r) => r.data);
  }

  delete(id: number): Promise<void> {
    return firstValueFrom(
      this.http.delete<ApiResponse<void>>(`${this.baseUrl}/${id}`),
    ).then(() => undefined);
  }
}
