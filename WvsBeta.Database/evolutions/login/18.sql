-- Add email verified column


------- UP -------
ALTER TABLE `users`
	ADD COLUMN `verified` TINYINT(1) NOT NULL DEFAULT 0 AFTER `email`;


------- DOWN -------
ALTER TABLE `users`   
	DROP COLUMN `verified`;
