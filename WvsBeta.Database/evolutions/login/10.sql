------- UP -------

ALTER TABLE `users`
ADD COLUMN `referral_code` VARCHAR(12) NULL DEFAULT NULL,
ADD COLUMN `referred_by` int NULL DEFAULT NULL;

------- DOWN -------

ALTER TABLE users 
DROP COLUMN `referral_code`,
DROP COLUMN `referred_by`,