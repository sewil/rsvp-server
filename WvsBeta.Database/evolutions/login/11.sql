
------- UP -------
ALTER TABLE `users`   
	ADD COLUMN `pin_secret` VARCHAR(255) NOT NULL AFTER `pin`;

------- DOWN -------

ALTER TABLE `users`   
	DROP COLUMN `pin_secret`;