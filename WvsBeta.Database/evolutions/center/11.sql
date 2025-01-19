------- UP -------

ALTER TABLE `cashshop_coupon_codes`   
	DROP COLUMN `nxcredit`, 
	DROP COLUMN `nxprepaid`, 
	CHANGE `serial` `couponcode` VARCHAR(30) CHARSET latin1 COLLATE latin1_swedish_ci NOT NULL;


ALTER TABLE `cashshop_coupon_item_rewards`   
	CHANGE `serial` `couponcode` VARCHAR(30) CHARSET latin1 COLLATE latin1_swedish_ci NOT NULL;


------- DOWN -------

ALTER TABLE `cashshop_coupon_codes`   
	ADD COLUMN `nxcredit` INT NOT NULL, 
	ADD COLUMN `nxprepaid` INT NOT NULL, 
	CHANGE `couponcode` `serial` VARCHAR(30) CHARSET latin1 COLLATE latin1_swedish_ci NOT NULL;

-- redo

ALTER TABLE `cashshop_coupon_item_rewards`   
	CHANGE `couponcode` `serial` VARCHAR(30) CHARSET latin1 COLLATE latin1_swedish_ci NOT NULL;
