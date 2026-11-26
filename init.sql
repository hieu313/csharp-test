
IF DB_ID('OrderManagement') IS NULL
    CREATE DATABASE OrderManagement;
GO
USE OrderManagement;
GO
CREATE TABLE Products (
    id         UNIQUEIDENTIFIER DEFAULT NEWID() NOT NULL PRIMARY KEY,
    name       NVARCHAR (255)   NOT NULL,
    sku        VARCHAR (50)     NOT NULL UNIQUE,
    price      DECIMAL (15, 2)  NOT NULL CHECK (price >= 0),
    stock      INT              DEFAULT 0 NOT NULL CHECK (stock >= 0),
    created_at DATETIME2        DEFAULT SYSUTCDATETIME() NOT NULL,
    updated_at DATETIME2        DEFAULT SYSUTCDATETIME() NOT NULL
);

CREATE TABLE Orders (
    id            UNIQUEIDENTIFIER DEFAULT NEWID() NOT NULL PRIMARY KEY,
    customer_name NVARCHAR (255)   NOT NULL,
    status        VARCHAR (20)     DEFAULT 'pending' NOT NULL CHECK (status IN (
                                                                  'pending',
                                                                  'confirmed',
                                                                  'completed',
                                                                  'cancelled'
                                                              )),
    total_amount  DECIMAL (15, 2)  DEFAULT 0 NOT NULL,
    created_at    DATETIME2        DEFAULT SYSUTCDATETIME() NOT NULL,
    updated_at    DATETIME2        DEFAULT SYSUTCDATETIME() NOT NULL
);

CREATE TABLE OrderDetails (
    id         UNIQUEIDENTIFIER DEFAULT NEWID() NOT NULL PRIMARY KEY,
    order_id   UNIQUEIDENTIFIER NOT NULL,
    product_id UNIQUEIDENTIFIER NOT NULL,
    quantity   INT              NOT NULL CHECK (quantity >= 1),
    unit_price DECIMAL (15, 2)  NOT NULL CHECK (unit_price >= 0),
    discount   DECIMAL (15, 2)  DEFAULT 0 NOT NULL CHECK (discount >= 0),
    subtotal   DECIMAL (15, 2)  NOT NULL CHECK (subtotal >= 0),
    notes      NVARCHAR (500)   NULL,
    created_at DATETIME2        DEFAULT SYSUTCDATETIME() NOT NULL,
    updated_at DATETIME2        DEFAULT SYSUTCDATETIME() NOT NULL,
    CONSTRAINT FK_OrderDetails_Orders FOREIGN KEY (order_id) REFERENCES Orders (id),
    CONSTRAINT FK_OrderDetails_Products FOREIGN KEY (product_id) REFERENCES Products (id)
);

CREATE INDEX IX_OrderDetails_OrderId
    ON OrderDetails(order_id);

CREATE INDEX IX_OrderDetails_ProductId
    ON OrderDetails(product_id);
-- khoá update lock -> các req get vẫn đọc được
ALTER DATABASE OrderManagement SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;

-- INSERT DATA --
INSERT INTO Products (id, name, sku, price, stock) VALUES
('00000000-0000-0000-0000-000000000001', N'Laptop Dell Inspiron 15 3520',        'LAP-DELL-INS15-3520',  14990000.00,  25),
('00000000-0000-0000-0000-000000000002', N'Laptop ASUS Vivobook 14 OLED',        'LAP-ASUS-VB14-OLED',   17490000.00,  18),
('00000000-0000-0000-0000-000000000003', N'MacBook Air M2 13 inch 256GB',        'LAP-APPL-MBA-M2-256',  24990000.00,  12),
('00000000-0000-0000-0000-000000000004', N'Chuột không dây Logitech M331',       'MOU-LOGI-M331',          299000.00, 150),
('00000000-0000-0000-0000-000000000005', N'Chuột gaming Razer DeathAdder V3',    'MOU-RAZR-DAV3',         1890000.00,  40),
('00000000-0000-0000-0000-000000000006', N'Bàn phím cơ Keychron K2 V2',          'KEY-KEYC-K2V2',         2290000.00,  35),
('00000000-0000-0000-0000-000000000007', N'Bàn phím Logitech K380 Bluetooth',    'KEY-LOGI-K380',          690000.00,  80),
('00000000-0000-0000-0000-000000000008', N'Màn hình LG UltraGear 27 inch 165Hz', 'MON-LG-UG27-165',       5990000.00,  20),
('00000000-0000-0000-0000-000000000009', N'Màn hình Dell U2723QE 27 inch 4K',    'MON-DELL-U2723QE',     12490000.00,   8),
('00000000-0000-0000-0000-000000000010', N'Tai nghe Sony WH-1000XM5',            'AUD-SONY-WH1000XM5',    7490000.00,  22),
('00000000-0000-0000-0000-000000000011', N'Tai nghe AirPods Pro 2',              'AUD-APPL-APP2',         5990000.00,  30),
('00000000-0000-0000-0000-000000000012', N'Loa Bluetooth JBL Flip 6',            'AUD-JBL-FLIP6',         2490000.00,  45),
('00000000-0000-0000-0000-000000000013', N'SSD Samsung 980 Pro 1TB NVMe',        'STO-SAMS-980P-1TB',     2790000.00,  60),
('00000000-0000-0000-0000-000000000014', N'SSD Kingston NV2 500GB NVMe',         'STO-KING-NV2-500',       999000.00,  90),
('00000000-0000-0000-0000-000000000015', N'Ổ cứng di động WD My Passport 2TB',   'STO-WD-MYPASS-2TB',     1990000.00,  55),
('00000000-0000-0000-0000-000000000016', N'Router Wi-Fi 6 TP-Link Archer AX55',  'NET-TPLK-AX55',         1590000.00,  38),
('00000000-0000-0000-0000-000000000017', N'Webcam Logitech C920 HD Pro',         'CAM-LOGI-C920',         1490000.00,  42),
('00000000-0000-0000-0000-000000000018', N'Hub USB-C Anker 7-in-1',              'ACC-ANKR-HUB7IN1',       990000.00,  70),
('00000000-0000-0000-0000-000000000019', N'Sạc dự phòng Anker 20000mAh 65W',     'ACC-ANKR-PB20K-65W',    1790000.00,  48),
('00000000-0000-0000-0000-000000000020', N'Balo laptop Targus 15.6 inch',        'ACC-TARG-BP156',         850000.00,   0);

INSERT INTO Orders (id, customer_name, status, total_amount, created_at, updated_at) VALUES
('10000000-0000-0000-0000-000000000001', N'Nguyễn Văn An',     'completed', 0, '2026-09-01T08:15:00', '2026-09-01T08:15:00'),
('10000000-0000-0000-0000-000000000002', N'Trần Thị Bình',     'completed', 0, '2026-09-03T10:30:00', '2026-09-03T10:30:00'),
('10000000-0000-0000-0000-000000000003', N'Lê Hoàng Cường',    'completed', 0, '2026-09-04T14:05:00', '2026-09-04T14:05:00'),
('10000000-0000-0000-0000-000000000004', N'Phạm Minh Đức',     'completed', 0, '2026-09-06T09:45:00', '2026-09-06T09:45:00'),
('10000000-0000-0000-0000-000000000005', N'Hoàng Thị Em',      'completed', 0, '2026-09-08T16:20:00', '2026-09-08T16:20:00'),
('10000000-0000-0000-0000-000000000006', N'Vũ Quang Huy',      'completed', 0, '2026-09-09T11:10:00', '2026-09-09T11:10:00'),
('10000000-0000-0000-0000-000000000007', N'Đặng Thu Hà',       'cancelled', 0, '2026-09-11T13:40:00', '2026-09-11T13:40:00'),
('10000000-0000-0000-0000-000000000008', N'Bùi Văn Khánh',     'completed', 0, '2026-09-12T19:00:00', '2026-09-12T19:00:00'),
('10000000-0000-0000-0000-000000000009', N'Đỗ Thị Lan',        'completed', 0, '2026-09-14T08:50:00', '2026-09-14T08:50:00'),
('10000000-0000-0000-0000-000000000010', N'Ngô Minh Long',     'completed', 0, '2026-09-15T15:25:00', '2026-09-15T15:25:00'),
('10000000-0000-0000-0000-000000000011', N'Dương Thị Mai',     'completed', 0, '2026-09-17T10:00:00', '2026-09-17T10:00:00'),
('10000000-0000-0000-0000-000000000012', N'Lý Văn Nam',        'completed', 0, '2026-09-18T17:35:00', '2026-09-18T17:35:00'),
('10000000-0000-0000-0000-000000000013', N'Phan Thị Ngọc',     'completed', 0, '2026-09-20T09:15:00', '2026-09-20T09:15:00'),
('10000000-0000-0000-0000-000000000014', N'Võ Hoàng Phúc',     'completed', 0, '2026-09-21T12:30:00', '2026-09-21T12:30:00'),
('10000000-0000-0000-0000-000000000015', N'Trịnh Thị Quỳnh',   'cancelled', 0, '2026-09-23T14:45:00', '2026-09-23T14:45:00'),
('10000000-0000-0000-0000-000000000016', N'Đinh Văn Sơn',      'completed', 0, '2026-09-24T18:10:00', '2026-09-24T18:10:00'),
('10000000-0000-0000-0000-000000000017', N'Lương Thị Thảo',    'completed', 0, '2026-09-26T08:20:00', '2026-09-26T08:20:00'),
('10000000-0000-0000-0000-000000000018', N'Mai Văn Tùng',      'completed', 0, '2026-09-28T11:55:00', '2026-09-28T11:55:00'),
('10000000-0000-0000-0000-000000000019', N'Tạ Thị Uyên',       'completed', 0, '2026-09-29T16:05:00', '2026-09-29T16:05:00'),
('10000000-0000-0000-0000-000000000020', N'Hồ Quốc Việt',      'completed', 0, '2026-10-01T09:30:00', '2026-10-01T09:30:00'),
('10000000-0000-0000-0000-000000000021', N'Nguyễn Thị Xuân',   'completed', 0, '2026-10-03T13:15:00', '2026-10-03T13:15:00'),
('10000000-0000-0000-0000-000000000022', N'Trần Đức Anh',      'completed', 0, '2026-10-04T10:40:00', '2026-10-04T10:40:00'),
('10000000-0000-0000-0000-000000000023', N'Lê Thị Bích',       'cancelled', 0, '2026-10-06T15:50:00', '2026-10-06T15:50:00'),
('10000000-0000-0000-0000-000000000024', N'Phạm Văn Chiến',    'completed', 0, '2026-10-08T08:05:00', '2026-10-08T08:05:00'),
('10000000-0000-0000-0000-000000000025', N'Hoàng Minh Dũng',   'completed', 0, '2026-10-09T19:20:00', '2026-10-09T19:20:00'),
('10000000-0000-0000-0000-000000000026', N'Vũ Thị Giang',      'completed', 0, '2026-10-11T11:35:00', '2026-10-11T11:35:00'),
('10000000-0000-0000-0000-000000000027', N'Đặng Văn Hải',      'completed', 0, '2026-10-13T14:00:00', '2026-10-13T14:00:00'),
('10000000-0000-0000-0000-000000000028', N'Bùi Thị Hương',     'completed', 0, '2026-10-14T09:10:00', '2026-10-14T09:10:00'),
('10000000-0000-0000-0000-000000000029', N'Đỗ Quang Hưng',     'cancelled', 0, '2026-10-16T16:45:00', '2026-10-16T16:45:00'),
('10000000-0000-0000-0000-000000000030', N'Ngô Thị Kim',       'completed', 0, '2026-10-18T10:25:00', '2026-10-18T10:25:00'),
('10000000-0000-0000-0000-000000000031', N'Dương Văn Lộc',     'completed', 0, '2026-10-19T13:50:00', '2026-10-19T13:50:00'),
('10000000-0000-0000-0000-000000000032', N'Lý Thị Minh',       'completed', 0, '2026-10-21T08:40:00', '2026-10-21T08:40:00'),
('10000000-0000-0000-0000-000000000033', N'Phan Văn Nghĩa',    'completed', 0, '2026-10-23T17:15:00', '2026-10-23T17:15:00'),
('10000000-0000-0000-0000-000000000034', N'Võ Thị Oanh',       'cancelled', 0, '2026-10-25T12:05:00', '2026-10-25T12:05:00'),
('10000000-0000-0000-0000-000000000035', N'Trịnh Văn Quân',    'completed', 0, '2026-10-27T15:30:00', '2026-10-27T15:30:00'),
('10000000-0000-0000-0000-000000000036', N'Đinh Thị Phương',   'completed', 0, '2026-10-29T09:55:00', '2026-10-29T09:55:00'),
('10000000-0000-0000-0000-000000000037', N'Lương Văn Quang',   'confirmed', 0, '2026-11-01T10:20:00', '2026-11-01T10:20:00'),
('10000000-0000-0000-0000-000000000038', N'Mai Thị Sen',       'completed', 0, '2026-11-03T14:35:00', '2026-11-03T14:35:00'),
('10000000-0000-0000-0000-000000000039', N'Tạ Văn Thắng',      'confirmed', 0, '2026-11-05T08:10:00', '2026-11-05T08:10:00'),
('10000000-0000-0000-0000-000000000040', N'Hồ Thị Trang',      'cancelled', 0, '2026-11-07T18:25:00', '2026-11-07T18:25:00'),
('10000000-0000-0000-0000-000000000041', N'Nguyễn Hữu Tuấn',   'confirmed', 0, '2026-11-09T11:45:00', '2026-11-09T11:45:00'),
('10000000-0000-0000-0000-000000000042', N'Trần Thị Vân',      'completed', 0, '2026-11-11T16:15:00', '2026-11-11T16:15:00'),
('10000000-0000-0000-0000-000000000043', N'Lê Văn Vinh',       'confirmed', 0, '2026-11-14T09:00:00', '2026-11-14T09:00:00'),
('10000000-0000-0000-0000-000000000044', N'Phạm Thị Yến',      'confirmed', 0, '2026-11-16T13:30:00', '2026-11-16T13:30:00'),
('10000000-0000-0000-0000-000000000045', N'Hoàng Văn Bảo',     'pending',   0, '2026-11-18T10:50:00', '2026-11-18T10:50:00'),
('10000000-0000-0000-0000-000000000046', N'Vũ Minh Châu',      'pending',   0, '2026-11-20T15:10:00', '2026-11-20T15:10:00'),
('10000000-0000-0000-0000-000000000047', N'Đặng Thị Diệp',     'pending',   0, '2026-11-22T08:30:00', '2026-11-22T08:30:00'),
('10000000-0000-0000-0000-000000000048', N'Bùi Hoàng Giang',   'pending',   0, '2026-11-23T19:40:00', '2026-11-23T19:40:00'),
('10000000-0000-0000-0000-000000000049', N'Đỗ Thị Hạnh',       'pending',   0, '2026-11-25T11:20:00', '2026-11-25T11:20:00'),
('10000000-0000-0000-0000-000000000050', N'Ngô Văn Khoa',      'pending',   0, '2026-11-25T17:05:00', '2026-11-25T17:05:00');

;WITH v (order_no, product_no, quantity, discount, notes) AS (
    SELECT * FROM (VALUES
    (1,  1,  1, 0,       NULL),
    (1,  4,  1, 0,       NULL),
    (2,  6,  1, 0,       NULL),
    (2,  7,  1, 50000,   NULL),
    (3,  3,  1, 500000,  N'Giảm giá khai trương'),
    (4,  8,  2, 0,       NULL),
    (5,  10, 1, 0,       NULL),
    (5,  18, 2, 0,       NULL),
    (6,  14, 3, 0,       NULL),
    (7,  2,  1, 0,       N'Khách hủy do đổi ý'),
    (8,  5,  1, 0,       NULL),
    (8,  4,  2, 0,       NULL),
    (8,  7,  1, 0,       NULL),
    (9,  11, 1, 0,       NULL),
    (10, 9,  1, 0,       NULL),
    (10, 17, 1, 0,       NULL),
    (11, 12, 2, 100000,  N'Khuyến mãi tháng 9'),
    (12, 15, 1, 0,       NULL),
    (12, 13, 1, 0,       NULL),
    (13, 16, 1, 0,       NULL),
    (14, 19, 2, 0,       NULL),
    (14, 18, 1, 0,       NULL),
    (15, 20, 1, 0,       N'Hết hàng, khách hủy'),
    (15, 4,  1, 0,       NULL),
    (16, 1,  2, 1000000, N'Mua số lượng cho văn phòng'),
    (17, 8,  1, 0,       NULL),
    (17, 5,  1, 0,       NULL),
    (18, 13, 2, 0,       NULL),
    (18, 14, 1, 0,       NULL),
    (19, 10, 1, 0,       NULL),
    (20, 6,  2, 0,       NULL),
    (20, 7,  2, 0,       NULL),
    (21, 17, 1, 0,       NULL),
    (21, 18, 1, 0,       NULL),
    (21, 4,  1, 0,       NULL),
    (22, 11, 2, 0,       NULL),
    (23, 3,  1, 0,       N'Khách hủy trước khi giao'),
    (24, 2,  1, 0,       NULL),
    (24, 19, 1, 0,       NULL),
    (25, 12, 1, 0,       NULL),
    (25, 15, 2, 0,       NULL),
    (26, 16, 2, 0,       NULL),
    (27, 9,  2, 500000,  N'Khách doanh nghiệp'),
    (28, 14, 2, 0,       NULL),
    (28, 13, 1, 0,       NULL),
    (29, 8,  1, 0,       NULL),
    (29, 20, 2, 0,       N'Hết hàng, khách hủy'),
    (30, 4,  3, 0,       NULL),
    (31, 10, 1, 0,       NULL),
    (31, 11, 1, 0,       NULL),
    (32, 5,  1, 0,       NULL),
    (32, 6,  1, 0,       NULL),
    (32, 17, 1, 0,       NULL),
    (33, 1,  1, 0,       NULL),
    (34, 12, 3, 0,       N'Khách hủy do trùng đơn'),
    (35, 15, 1, 0,       NULL),
    (35, 18, 3, 0,       NULL),
    (36, 19, 2, 0,       NULL),
    (37, 2,  1, 0,       NULL),
    (37, 4,  1, 0,       NULL),
    (38, 13, 2, 0,       NULL),
    (39, 8,  2, 0,       NULL),
    (39, 7,  1, 0,       NULL),
    (40, 9,  1, 0,       N'Khách hủy'),
    (41, 11, 1, 0,       NULL),
    (41, 12, 1, 0,       NULL),
    (42, 3,  1, 0,       NULL),
    (43, 16, 1, 0,       NULL),
    (43, 14, 2, 0,       NULL),
    (44, 10, 1, 0,       NULL),
    (45, 1,  1, 0,       NULL),
    (46, 5,  1, 0,       NULL),
    (46, 6,  1, 0,       NULL),
    (47, 17, 2, 0,       NULL),
    (48, 18, 1, 0,       NULL),
    (48, 19, 1, 0,       NULL),
    (48, 4,  1, 0,       NULL),
    (49, 15, 1, 0,       NULL),
    (50, 13, 1, 0,       NULL),
    (50, 14, 1, 0,       NULL)
    ) AS t (order_no, product_no, quantity, discount, notes)
)
INSERT INTO OrderDetails (order_id, product_id, quantity, unit_price, discount, subtotal, notes, created_at, updated_at)
SELECT
    o.id,
    p.id,
    v.quantity,
    p.price,
    v.discount,
    v.quantity * p.price - v.discount,
    v.notes,
    o.created_at,
    o.created_at
FROM v
JOIN Orders   o ON o.id = CAST('10000000-0000-0000-0000-' + RIGHT('000000000000' + CAST(v.order_no   AS VARCHAR(12)), 12) AS UNIQUEIDENTIFIER)
JOIN Products p ON p.id = CAST('00000000-0000-0000-0000-' + RIGHT('000000000000' + CAST(v.product_no AS VARCHAR(12)), 12) AS UNIQUEIDENTIFIER);

UPDATE o
SET total_amount = d.total,
    updated_at   = o.created_at
FROM Orders o
JOIN (
    SELECT order_id, SUM(subtotal) AS total
    FROM OrderDetails
    GROUP BY order_id
) d ON d.order_id = o.id;

-- Mỗi đơn có ít nhất 1 dòng chi tiết, tổng khớp
SELECT COUNT(*) AS orders_without_details
FROM Orders o
WHERE NOT EXISTS (SELECT 1 FROM OrderDetails d WHERE d.order_id = o.id);   -- kỳ vọng: 0

SELECT COUNT(*) AS bad_subtotal
FROM OrderDetails
WHERE subtotal <> quantity * unit_price - discount;                         -- kỳ vọng: 0