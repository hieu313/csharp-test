- API với MVC
- Error handler
- Thêm repository: tách thao tác theo entity
- Unit of Work: quản lý các repository dùng chung 1 dbcontext và quyết định khi nào commit
- Tách thành CQRS
- Thêm bus mediar
- CancellationToken truyền xuống repository để hủy operation đang chạy khi client ngắt kết nối. Nếu không truyền ct xuống, dù client đã ngắt, server vẫn tiếp tục chạy query DB, tính toán, commit transaction.
- merge command + handler theo nest class
- Tối ưu với error code
- S trong SOLID (1 handle xử lý trong CQRS thường là 3 phần: validate (params, business logic), handle logic, return response)
- FluentValidation

2. Vòng đời req

- Pipeline theo kiểu lồng nhau nên res đi ngược lại qua các middleware

```
Client
  │
  ▼
Kestrel (web server)
  │
  ▼
Middleware pipeline (theo thứ tự đăng ký)
  ├─ ExceptionHandler
  ├─ HTTPS Redirection
  ├─ Static Files
  ├─ Routing            ← match endpoint
  ├─ CORS
  ├─ Authentication     ← xác định "bạn là ai"
  ├─ Authorization      ← xác định "bạn được làm gì"
  └─ Endpoint execution
        │
        ▼
   Filter pipeline (MVC/Controller)
     Authorization → Resource → [Model Binding + Validation]
     → Action Filter → Action → Result Filter
        │
        ▼
   Controller Action → Service / MediatR Handler → Repository (EF Core)
        │
        ▼
   Response đi ngược lại qua các middleware
```

```
docker compose up -d
docker cp init.sql sqlserver:/init.sql
docker exec sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "YourStrong@Passw0rd" -i /init.sql

```
