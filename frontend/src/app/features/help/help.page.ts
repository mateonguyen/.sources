import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { AccordionModule } from 'primeng/accordion';
import { SectionCardComponent } from '../../shared/ui/section-card.component';

interface FaqItem {
  q: string;
  a: string;
  open: boolean;
}

@Component({
  selector: 'app-help-page',
  standalone: true,
  imports: [CommonModule, SectionCardComponent, AccordionModule],
  templateUrl: './help.page.html',
  styleUrl: './help.page.scss',
})
export class HelpPage {
  faqItems: FaqItem[] = [
    {
      q: 'Tôi quên mật khẩu, phải làm gì?',
      a: 'Vui lòng liên hệ quản trị viên hệ thống để được cấp lại mật khẩu. Quản trị viên có thể đặt lại mật khẩu cho tài khoản của bạn qua màn hình Quản lý người dùng.',
      open: false,
    },
    {
      q: 'Tại sao tôi không truy cập được một số màn hình?',
      a: 'Quyền truy cập được phân theo vai trò. Nếu bạn cần quyền truy cập vào một phân hệ, hãy liên hệ quản trị viên để được cấp quyền phù hợp.',
      open: false,
    },
    {
      q: 'Dữ liệu tôi nhập có được lưu tự động không?',
      a: 'Hệ thống không tự động lưu. Bạn cần nhấn nút "Lưu" hoặc "Xác nhận" sau khi nhập liệu. Nếu thoát màn hình mà chưa lưu, dữ liệu sẽ bị mất.',
      open: false,
    },
    {
      q: 'Làm sao để xuất dữ liệu ra file Excel/PDF?',
      a: 'Các màn hình có hỗ trợ xuất file sẽ có nút "Xuất Excel" hoặc "Xuất PDF" trên thanh công cụ. Nếu không thấy nút này, chức năng đó có thể chưa được kích hoạt cho tài khoản của bạn.',
      open: false,
    },
    {
      q: 'Phiên làm việc của tôi bị hết hạn, phải làm gì?',
      a: 'Hệ thống sẽ tự động chuyển về trang đăng nhập khi phiên hết hạn (thường sau 8 giờ không hoạt động). Chỉ cần đăng nhập lại là tiếp tục được.',
      open: false,
    },
  ];

  toggleFaq(item: FaqItem): void {
    item.open = !item.open;
  }
}
