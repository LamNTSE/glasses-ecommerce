-- Chạy một lần trên PostgreSQL: đổi orders.created_at từ DATE sang timestamptz
-- để lưu đúng giờ tạo đơn (mới nhất trước khi sort).
--
-- Nếu cột hiện là `date`:
ALTER TABLE orders
  ALTER COLUMN created_at TYPE timestamp with time zone
  USING (created_at::timestamp AT TIME ZONE 'UTC');

-- Nếu lệnh trên báo lỗi kiểu cột, kiểm tra: \d orders
