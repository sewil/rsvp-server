------- UP -------

ALTER TABLE `guilds`   
	ADD COLUMN `logo_bg` INT NOT NULL DEFAULT 0 AFTER `capacity`,
	ADD COLUMN `logo_bg_color` INT NOT NULL DEFAULT 0 AFTER `logo_bg`,
	ADD COLUMN `logo_fg` INT NOT NULL DEFAULT 0 AFTER `logo_bg_color`,
	ADD COLUMN `logo_fg_color` INT NOT NULL DEFAULT 0 AFTER `logo_fg`;

------- DOWN -------

ALTER TABLE `guilds`   
	DROP COLUMN `logo_bg`,
	DROP COLUMN `logo_bg_color`,
	DROP COLUMN `logo_fg`,
	DROP COLUMN `logo_fg_color`;