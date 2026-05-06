import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api.constants';

interface ApiResponse<T> {
  success: boolean;
  data?: T;
}

export interface TienDoDonViDto {
  donViId: number;
  tenDonVi: string;
  capDonVi: string;
  daXacNhan: boolean;
  capNhatLanCuoi: string | null;
  soNhanLuc: number;
  soThietBi: number;
  soHeThongThongTin: number;
  soHaTangMang: number;
  soDaoTao: number;
  soDuAn: number;
}

@Injectable({ providedIn: 'root' })
export class TongHopTienDoApi {
  private readonly base = `${API_BASE_URL}/tong-hop-tien-do`;

  constructor(private readonly http: HttpClient) {}

  getTienDo(
    kyBaoCaoCode: string,
    parentDonViId?: number,
  ): Promise<TienDoDonViDto[]> {
    let params = new HttpParams().set('kyBaoCaoCode', kyBaoCaoCode);
    if (parentDonViId != null)
      params = params.set('parentDonViId', parentDonViId);
    return firstValueFrom(
      this.http.get<ApiResponse<TienDoDonViDto[]>>(this.base, { params }),
    ).then((r) => r.data ?? []);
  }

  getMyTienDo(kyBaoCaoCode: string): Promise<TienDoDonViDto> {
    const params = new HttpParams().set('kyBaoCaoCode', kyBaoCaoCode);
    return firstValueFrom(
      this.http.get<ApiResponse<TienDoDonViDto>>(`${this.base}/my-tien-do`, {
        params,
      }),
    ).then((r) => r.data!);
  }

  xacNhan(kyBaoCaoCode: string, daXacNhan: boolean): Promise<void> {
    return firstValueFrom(
      this.http.post<ApiResponse<null>>(`${this.base}/xac-nhan`, {
        kyBaoCaoCode,
        daXacNhan,
      }),
    ).then(() => void 0);
  }
}
