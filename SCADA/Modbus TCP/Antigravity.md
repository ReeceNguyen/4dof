# Cấu trúc dự án

## Models
- RobotParameters.cs       Chứa góc khớp (Q1-Q4) và tọa độ (X, Y, Z, Roll/Yaw)
- ConnectionConfig.cs      Chứa IP, Port, Watchdog Timeout
## Services
1. Kết nối bằng giao thức TCP/IP; đọc/ghi dữ liệu Modbus TCP tới OPTA Codesys
2. Kiểm tra hear beat và tình trạng kết nối
3. Hàm tính động học robot cánh tay 4DOF
     - Hàm tính toán bảng D-H
     - Động học thuận
     - Động học ngược
     - Động lực học
     - Ma trận Jacoby
     - Thiết kế quỹ đạo và trajectory 
4. Storage Database: SQLite Ghi log lịch sử vận hành local
## ViewModels
- MainViewModel.cs Binding dữ liệu giữa UI, Services
## View
- Giao diện hiển thị và nhập liệu chia theo tab:
    - Tab1: Kiểm tra kết nối: 
      - Nhập IP phần cứng muốn kết nối
      - 2 nút kết nối và ngăt kết nối
      - Textbox hiện báo đã kết nối hoặc mất kết nối
    - Tab2: Giao diện hiển thị và nhập liệu
        - 1 khung dành cho nhập liệu, hiển thị số liệu
        - 1 khung là hiển thị các đồ thị và mô hình mô phỏng chuyển động cánh tay robot, các khớp , cánh tay vẽ bằng hình 2D cũng được
## Package
Tự cài các thư viện cần thiết cho dự án