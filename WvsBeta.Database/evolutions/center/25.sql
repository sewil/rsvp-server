-- Fix tinyint(1) used for non-booleans

------- UP -------
ALTER TABLE `characters`   
  CHANGE `world_id` `world_id` TINYINT(4) NOT NULL;

ALTER TABLE `itemlocker`   
  CHANGE `gift_unread` `gift_unread` TINYINT(1) DEFAULT 0  NOT NULL;


------- DOWN -------
ALTER TABLE `characters`   
  CHANGE `world_id` `world_id` TINYINT(1) UNSIGNED NOT NULL;

ALTER TABLE `itemlocker`   
  CHANGE `gift_unread` `gift_unread` TINYINT(4) DEFAULT 0  NOT NULL;
