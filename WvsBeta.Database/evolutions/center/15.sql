------- UP -------

ALTER TABLE `characters` ADD COLUMN `guild_rank` INT(10) UNSIGNED DEFAULT 0 NULL AFTER `guild_id`; 

UPDATE characters c JOIN guilds g ON g.id = c.guild_id SET c.guild_rank = IF(g.guildmaster_id = c.id, 3, 1);

------- DOWN -------

ALTER TABLE `characters` DROP COLUMN guild_rank;
