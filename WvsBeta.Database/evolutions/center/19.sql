-- Cleanup unused columns and tables, also add created_at and updated_at columns


------- UP -------
ALTER TABLE `characters`   
	DROP COLUMN `online`, 
	DROP COLUMN `time_level`, 
	DROP COLUMN `event`, 
	DROP COLUMN `eventmap`, 
	DROP COLUMN `party`, 
	DROP COLUMN `hash`, 
	ADD COLUMN `deleted_at` DATETIME NULL AFTER `guild_rank`,
    ADD COLUMN `created_at` timestamp NULL DEFAULT current_timestamp(),
    ADD COLUMN `updated_at` timestamp NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(), 
    DROP INDEX `online`;

DROP TABLE IF EXISTS data_ids, inc_table, connections, password_resets, jobs, items, cooldowns, completed_quests, 
character_cashshop_gifts, cashshop_sell_log, cashshop_modified_items, cashshop_limit_sell, beta_keys, storage_items, storage_cashshop, pets, character_quest_mobs;

ALTER TABLE `character_quests`   
	DROP COLUMN `id`, 
  DROP PRIMARY KEY;


------- DOWN -------

ALTER TABLE `characters`
  ADD COLUMN `online` tinyint(1) NOT NULL DEFAULT 0,
  ADD COLUMN `time_level` datetime NOT NULL DEFAULT '2012-01-09 12:37:00',
  ADD COLUMN `event` datetime NOT NULL DEFAULT '2012-01-09 12:37:00',
  ADD COLUMN `eventmap` int(11) unsigned NOT NULL DEFAULT 0,
  ADD COLUMN `party` int(11) NOT NULL DEFAULT -1,
  ADD COLUMN `hash` varchar(50) COLLATE latin1_general_ci DEFAULT NULL,
  DROP COLUMN `deleted_at`, 
  DROP COLUMN `created_at`, 
  DROP COLUMN `updated_at`, 
  ADD INDEX `online` (`online`);
  
ALTER TABLE `character_quests`   
	ADD COLUMN `id` int(11) NOT NULL AUTO_INCREMENT, 
  ADD PRIMARY KEY (id);
