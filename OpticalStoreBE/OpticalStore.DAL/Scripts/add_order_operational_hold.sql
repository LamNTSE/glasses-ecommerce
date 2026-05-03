-- Chạy thủ công trên DB (PostgreSQL) khi deploy ON_HOLD cho đơn hàng.

ALTER TABLE orders ADD COLUMN IF NOT EXISTS operational_hold_reason TEXT;

ALTER TABLE orders ADD COLUMN IF NOT EXISTS status_before_hold VARCHAR(255);
