------- UP -------

ALTER TABLE users 
ADD COLUMN superadmin TINYINT(1) DEFAULT 0,
ADD COLUMN beta TINYINT(1) DEFAULT 0;

------- DOWN -------

ALTER TABLE users 
DROP COLUMN superadmin,
DROP COLUMN beta;