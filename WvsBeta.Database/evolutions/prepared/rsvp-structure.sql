/*
SQLyog Community v12.01 (64 bit)
MySQL - 10.3.39-MariaDB : Database - rsvp
*********************************************************************
*/

/*!40101 SET NAMES utf8 */;

/*!40101 SET SQL_MODE=''*/;

/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;
CREATE DATABASE /*!32312 IF NOT EXISTS*/`rsvp` /*!40100 DEFAULT CHARACTER SET latin1 COLLATE latin1_swedish_ci */;

USE `rsvp`;

/*Table structure for table `buddylist` */

DROP TABLE IF EXISTS `buddylist`;

CREATE TABLE `buddylist` (
  `charid` int(11) NOT NULL,
  `buddy_charid` int(11) NOT NULL,
  `buddy_charname` varchar(12) NOT NULL,
  UNIQUE KEY `charid` (`charid`,`buddy_charid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `buddylist_pending` */

DROP TABLE IF EXISTS `buddylist_pending`;

CREATE TABLE `buddylist_pending` (
  `charid` int(11) NOT NULL,
  `inviter_charid` int(11) NOT NULL,
  `inviter_charname` varchar(13) NOT NULL
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `cashitem_bundle` */

DROP TABLE IF EXISTS `cashitem_bundle`;

CREATE TABLE `cashitem_bundle` (
  `userid` int(11) NOT NULL,
  `itemid` int(11) NOT NULL,
  `amount` smallint(6) NOT NULL DEFAULT 1,
  `cashid` bigint(20) NOT NULL,
  `expiration` bigint(20) NOT NULL DEFAULT 150842304000000000,
  PRIMARY KEY (`cashid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*Table structure for table `cashitem_eqp` */

DROP TABLE IF EXISTS `cashitem_eqp`;

CREATE TABLE `cashitem_eqp` (
  `userid` int(11) NOT NULL,
  `itemid` int(11) NOT NULL,
  `slots` tinyint(4) NOT NULL DEFAULT 7,
  `scrolls` tinyint(4) NOT NULL DEFAULT 0,
  `istr` smallint(6) NOT NULL DEFAULT 0,
  `idex` smallint(6) NOT NULL DEFAULT 0,
  `iint` smallint(6) NOT NULL DEFAULT 0,
  `iluk` smallint(6) NOT NULL DEFAULT 0,
  `ihp` smallint(6) NOT NULL DEFAULT 0,
  `imp` smallint(6) NOT NULL DEFAULT 0,
  `iwatk` smallint(6) NOT NULL DEFAULT 0,
  `imatk` smallint(6) NOT NULL DEFAULT 0,
  `iwdef` smallint(6) NOT NULL DEFAULT 0,
  `imdef` smallint(6) NOT NULL DEFAULT 0,
  `iacc` smallint(6) NOT NULL DEFAULT 0,
  `iavo` smallint(6) NOT NULL DEFAULT 0,
  `ihand` smallint(6) NOT NULL DEFAULT 0,
  `ispeed` smallint(6) NOT NULL DEFAULT 0,
  `ijump` smallint(6) NOT NULL DEFAULT 0,
  `cashid` bigint(20) NOT NULL,
  `expiration` bigint(20) NOT NULL DEFAULT 150842304000000000,
  PRIMARY KEY (`cashid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*Table structure for table `cashitem_pet` */

DROP TABLE IF EXISTS `cashitem_pet`;

CREATE TABLE `cashitem_pet` (
  `userid` int(11) NOT NULL,
  `cashid` bigint(20) NOT NULL,
  `itemid` int(11) NOT NULL,
  `name` varchar(12) NOT NULL,
  `level` tinyint(3) NOT NULL,
  `closeness` smallint(6) NOT NULL,
  `fullness` tinyint(3) NOT NULL,
  `expiration` bigint(20) NOT NULL,
  `deaddate` bigint(20) NOT NULL,
  PRIMARY KEY (`cashid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*Table structure for table `cashshop_coupon_codes` */

DROP TABLE IF EXISTS `cashshop_coupon_codes`;

CREATE TABLE `cashshop_coupon_codes` (
  `couponcode` varchar(30) NOT NULL,
  `maplepoints` int(11) NOT NULL DEFAULT 0,
  `mesos` int(11) NOT NULL DEFAULT 0,
  `used` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`couponcode`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `cashshop_coupon_item_rewards` */

DROP TABLE IF EXISTS `cashshop_coupon_item_rewards`;

CREATE TABLE `cashshop_coupon_item_rewards` (
  `couponcode` varchar(30) NOT NULL,
  `itemid` int(11) NOT NULL,
  `amount` int(11) NOT NULL DEFAULT 1,
  `days_usable` int(5) NOT NULL DEFAULT 0
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `character_quests` */

DROP TABLE IF EXISTS `character_quests`;

CREATE TABLE `character_quests` (
  `charid` int(11) NOT NULL,
  `questid` int(16) NOT NULL,
  `data` varchar(40) DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `character_rate_credits` */

DROP TABLE IF EXISTS `character_rate_credits`;

CREATE TABLE `character_rate_credits` (
  `charid` int(11) NOT NULL,
  `uid` bigint(20) NOT NULL,
  `type` enum('exp','drop','mesos') NOT NULL,
  `rate` double NOT NULL,
  `credits_left` int(11) NOT NULL,
  `credits_given` int(11) NOT NULL,
  `rolls` int(11) NOT NULL,
  `comment` text NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT current_timestamp(),
  `enabled` tinyint(1) DEFAULT 0,
  PRIMARY KEY (`charid`,`uid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

/*Table structure for table `character_variables` */

DROP TABLE IF EXISTS `character_variables`;

CREATE TABLE `character_variables` (
  `charid` int(11) NOT NULL,
  `key` varchar(255) NOT NULL,
  `value` varchar(255) NOT NULL,
  UNIQUE KEY `charid_2` (`charid`,`key`),
  KEY `charid` (`charid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `character_wishlist` */

DROP TABLE IF EXISTS `character_wishlist`;

CREATE TABLE `character_wishlist` (
  `charid` int(11) NOT NULL,
  `serial` int(11) NOT NULL
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `characters` */

DROP TABLE IF EXISTS `characters`;

CREATE TABLE `characters` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `name` varchar(12) NOT NULL,
  `userid` int(11) NOT NULL,
  `world_id` tinyint(1) unsigned NOT NULL,
  `level` tinyint(3) unsigned NOT NULL DEFAULT 1,
  `job` smallint(6) NOT NULL DEFAULT 0,
  `str` smallint(6) NOT NULL DEFAULT 4,
  `dex` smallint(6) NOT NULL DEFAULT 4,
  `int` smallint(6) NOT NULL DEFAULT 4,
  `luk` smallint(6) NOT NULL DEFAULT 4,
  `chp` smallint(6) NOT NULL DEFAULT 50,
  `mhp` smallint(6) NOT NULL DEFAULT 50,
  `cmp` smallint(6) NOT NULL DEFAULT 50,
  `mmp` smallint(6) NOT NULL DEFAULT 50,
  `hpmp_ap` int(11) NOT NULL DEFAULT 0,
  `ap` smallint(6) NOT NULL DEFAULT 0,
  `sp` smallint(6) NOT NULL DEFAULT 0,
  `exp` int(11) NOT NULL DEFAULT 0,
  `fame` smallint(6) NOT NULL DEFAULT 0,
  `map` int(11) NOT NULL DEFAULT 0,
  `pos` smallint(6) NOT NULL DEFAULT 0,
  `gender` tinyint(1) NOT NULL,
  `skin` tinyint(4) NOT NULL,
  `eyes` int(11) NOT NULL,
  `hair` int(11) NOT NULL,
  `mesos` int(11) NOT NULL DEFAULT 0,
  `equip_slots` int(11) NOT NULL DEFAULT 24,
  `use_slots` int(11) NOT NULL DEFAULT 24,
  `setup_slots` int(11) NOT NULL DEFAULT 24,
  `etc_slots` int(11) NOT NULL DEFAULT 24,
  `cash_slots` int(11) NOT NULL DEFAULT 48,
  `buddylist_size` int(3) unsigned NOT NULL DEFAULT 20,
  `overall_cpos` int(11) NOT NULL DEFAULT 0,
  `overall_opos` int(11) NOT NULL DEFAULT 0,
  `world_cpos` int(11) NOT NULL DEFAULT 0,
  `world_opos` int(11) NOT NULL DEFAULT 0,
  `job_cpos` int(11) NOT NULL DEFAULT 0,
  `job_opos` int(11) NOT NULL DEFAULT 0,
  `fame_cpos` int(11) NOT NULL DEFAULT 0,
  `fame_opos` int(11) NOT NULL DEFAULT 0,
  `last_savepoint` datetime DEFAULT '2012-01-09 12:37:00',
  `rankbanned` tinyint(1) NOT NULL DEFAULT 0 COMMENT '0 false 1 true',
  `pet_cash_id` bigint(20) NOT NULL DEFAULT 0,
  `guild_id` int(10) unsigned DEFAULT NULL,
  `guild_rank` int(10) unsigned DEFAULT 0,
  `deleted_at` datetime DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  PRIMARY KEY (`ID`),
  KEY `userid` (`userid`),
  KEY `world_id` (`world_id`),
  KEY `name` (`name`),
  KEY `job` (`job`),
  KEY `level` (`level`),
  KEY `fame` (`fame`),
  KEY `userid_worldid` (`userid`,`world_id`),
  KEY `world_ranking` (`world_cpos`)
) ENGINE=MyISAM AUTO_INCREMENT=46270 DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*Table structure for table `fame_log` */

DROP TABLE IF EXISTS `fame_log`;

CREATE TABLE `fame_log` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `from` int(11) NOT NULL,
  `to` int(11) NOT NULL,
  `time` datetime NOT NULL,
  PRIMARY KEY (`id`),
  KEY `from` (`from`,`to`,`time`)
) ENGINE=MyISAM AUTO_INCREMENT=68333 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `gamestats` */

DROP TABLE IF EXISTS `gamestats`;

CREATE TABLE `gamestats` (
  `id` int(11) NOT NULL DEFAULT 0,
  `omokwins` int(11) NOT NULL DEFAULT 0,
  `omoklosses` int(11) NOT NULL DEFAULT 0,
  `omokscore` int(11) NOT NULL DEFAULT 2000,
  `omokties` int(11) NOT NULL DEFAULT 0,
  `matchcardwins` int(11) NOT NULL DEFAULT 0,
  `matchcardties` int(11) NOT NULL DEFAULT 0,
  `matchcardlosses` int(11) NOT NULL DEFAULT 0,
  `matchcardscore` int(11) NOT NULL DEFAULT 2000,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `guilds` */

DROP TABLE IF EXISTS `guilds`;

CREATE TABLE `guilds` (
  `id` int(10) unsigned NOT NULL AUTO_INCREMENT,
  `name` varchar(255) DEFAULT NULL,
  `guildmaster_id` int(10) unsigned DEFAULT NULL,
  `capacity` int(10) unsigned NOT NULL DEFAULT 10,
  `logo_bg` int(11) NOT NULL DEFAULT 0,
  `logo_bg_color` int(11) NOT NULL DEFAULT 0,
  `logo_fg` int(11) NOT NULL DEFAULT 0,
  `logo_fg_color` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=32 DEFAULT CHARSET=utf8 COLLATE=utf8_general_ci;

/*Table structure for table `inventory_bundle` */

DROP TABLE IF EXISTS `inventory_bundle`;

CREATE TABLE `inventory_bundle` (
  `charid` int(11) NOT NULL,
  `inv` tinyint(4) NOT NULL,
  `slot` smallint(6) NOT NULL,
  `itemid` int(11) NOT NULL,
  `amount` int(11) NOT NULL DEFAULT 1,
  `cashid` bigint(20) NOT NULL DEFAULT 0,
  `expiration` bigint(20) NOT NULL DEFAULT 150842304000000000,
  PRIMARY KEY (`charid`,`inv`,`slot`),
  KEY `charid` (`charid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*Table structure for table `inventory_eqp` */

DROP TABLE IF EXISTS `inventory_eqp`;

CREATE TABLE `inventory_eqp` (
  `charid` int(11) NOT NULL,
  `slot` smallint(6) NOT NULL,
  `itemid` int(11) NOT NULL,
  `slots` tinyint(4) NOT NULL DEFAULT 7,
  `scrolls` tinyint(4) NOT NULL DEFAULT 0,
  `istr` smallint(6) NOT NULL DEFAULT 0,
  `idex` smallint(6) NOT NULL DEFAULT 0,
  `iint` smallint(6) NOT NULL DEFAULT 0,
  `iluk` smallint(6) NOT NULL DEFAULT 0,
  `ihp` smallint(6) NOT NULL DEFAULT 0,
  `imp` smallint(6) NOT NULL DEFAULT 0,
  `iwatk` smallint(6) NOT NULL DEFAULT 0,
  `imatk` smallint(6) NOT NULL DEFAULT 0,
  `iwdef` smallint(6) NOT NULL DEFAULT 0,
  `imdef` smallint(6) NOT NULL DEFAULT 0,
  `iacc` smallint(6) NOT NULL DEFAULT 0,
  `iavo` smallint(6) NOT NULL DEFAULT 0,
  `ihand` smallint(6) NOT NULL DEFAULT 0,
  `ispeed` smallint(6) NOT NULL DEFAULT 0,
  `ijump` smallint(6) NOT NULL DEFAULT 0,
  `cashid` bigint(20) NOT NULL DEFAULT 0,
  `expiration` bigint(20) NOT NULL DEFAULT 150842304000000000,
  PRIMARY KEY (`charid`,`slot`),
  KEY `charid` (`charid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*Table structure for table `ipbans` */

DROP TABLE IF EXISTS `ipbans`;

CREATE TABLE `ipbans` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `ip` varchar(15) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM AUTO_INCREMENT=119 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `itemlocker` */

DROP TABLE IF EXISTS `itemlocker`;

CREATE TABLE `itemlocker` (
  `cashid` bigint(20) NOT NULL,
  `slot` smallint(6) NOT NULL,
  `userid` int(11) NOT NULL,
  `characterid` int(11) NOT NULL,
  `itemid` int(11) NOT NULL,
  `commodity_id` int(11) NOT NULL,
  `amount` smallint(6) NOT NULL,
  `buycharactername` varchar(13) NOT NULL,
  `expiration` bigint(20) NOT NULL,
  `gift_unread` tinyint(4) NOT NULL DEFAULT 0,
  `worldid` tinyint(4) NOT NULL,
  PRIMARY KEY (`cashid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `login_records` */

DROP TABLE IF EXISTS `login_records`;

CREATE TABLE `login_records` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `userid` int(11) DEFAULT NULL,
  `uniqueid` varchar(26) CHARACTER SET latin1 COLLATE latin1_general_ci DEFAULT NULL,
  `windows_username` varchar(255) DEFAULT NULL,
  `windows_machine_name` varchar(255) DEFAULT NULL,
  `local_userid` varchar(56) DEFAULT NULL,
  `first_login` datetime DEFAULT NULL,
  `last_login` datetime DEFAULT NULL,
  `login_count` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `unique_pc_data` (`userid`,`uniqueid`,`local_userid`,`windows_username`,`windows_machine_name`),
  KEY `userid_uniqueid` (`userid`,`uniqueid`),
  KEY `userid` (`userid`)
) ENGINE=InnoDB AUTO_INCREMENT=204146 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `machine_ban` */

DROP TABLE IF EXISTS `machine_ban`;

CREATE TABLE `machine_ban` (
  `machineid` varchar(32) NOT NULL,
  `last_username` varchar(13) NOT NULL,
  `last_ip` varchar(15) NOT NULL,
  `last_try` datetime NOT NULL,
  `reason` text DEFAULT NULL,
  `last_unique_id` varchar(26) DEFAULT '',
  UNIQUE KEY `machineid` (`machineid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `memos` */

DROP TABLE IF EXISTS `memos`;

CREATE TABLE `memos` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `from_name` varchar(13) NOT NULL,
  `to_charid` int(11) NOT NULL,
  `message` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
  `sent_time` timestamp NOT NULL DEFAULT current_timestamp(),
  `read_time` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=6992 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `monsterbook` */

DROP TABLE IF EXISTS `monsterbook`;

CREATE TABLE `monsterbook` (
  `charid` int(11) NOT NULL,
  `monsterbookid` int(11) NOT NULL,
  `count` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`charid`,`monsterbookid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `rings` */

DROP TABLE IF EXISTS `rings`;

CREATE TABLE `rings` (
  `ringid` int(10) NOT NULL,
  `itemid` int(15) NOT NULL,
  `charid` int(10) NOT NULL,
  `partnerid` int(10) NOT NULL,
  `equipped` int(1) NOT NULL
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `server_evolutions_center` */

DROP TABLE IF EXISTS `server_evolutions_center`;

CREATE TABLE `server_evolutions_center` (
  `id` int(11) NOT NULL,
  `script_up` longtext NOT NULL,
  `script_down` longtext DEFAULT NULL,
  `file_hash` varchar(128) NOT NULL,
  `apply_date` datetime NOT NULL,
  `state` varchar(45) NOT NULL,
  `last_error` longtext NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `server_evolutions_login` */

DROP TABLE IF EXISTS `server_evolutions_login`;

CREATE TABLE `server_evolutions_login` (
  `id` int(11) NOT NULL,
  `script_up` longtext NOT NULL,
  `script_down` longtext DEFAULT NULL,
  `file_hash` varchar(128) NOT NULL,
  `apply_date` datetime NOT NULL,
  `state` varchar(45) NOT NULL,
  `last_error` longtext NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `servers` */

DROP TABLE IF EXISTS `servers`;

CREATE TABLE `servers` (
  `configname` varchar(15) DEFAULT NULL,
  `world_id` tinyint(4) DEFAULT NULL,
  `private_ip` varchar(15) DEFAULT NULL
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `skills` */

DROP TABLE IF EXISTS `skills`;

CREATE TABLE `skills` (
  `charid` int(11) NOT NULL,
  `skillid` int(11) NOT NULL,
  `points` smallint(6) NOT NULL DEFAULT 1,
  UNIQUE KEY `charid_2` (`charid`,`skillid`),
  KEY `charid` (`charid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*Table structure for table `storage` */

DROP TABLE IF EXISTS `storage`;

CREATE TABLE `storage` (
  `userid` int(11) NOT NULL,
  `world_id` int(11) NOT NULL,
  `slots` smallint(6) NOT NULL DEFAULT 4,
  `mesos` int(11) NOT NULL DEFAULT 0,
  `char_slots` int(11) NOT NULL DEFAULT 3,
  `credit_nx` int(11) NOT NULL DEFAULT 0,
  `maplepoints` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`userid`,`world_id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `storage_bundle` */

DROP TABLE IF EXISTS `storage_bundle`;

CREATE TABLE `storage_bundle` (
  `userid` int(11) NOT NULL,
  `world_id` int(11) NOT NULL,
  `inv` tinyint(4) NOT NULL,
  `slot` smallint(6) NOT NULL,
  `itemid` int(11) NOT NULL,
  `amount` smallint(11) NOT NULL DEFAULT 1,
  `cashid` bigint(20) NOT NULL,
  `expiration` bigint(20) NOT NULL DEFAULT 150842304000000000,
  KEY `userid_worldid` (`userid`,`world_id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*Table structure for table `storage_cash` */

DROP TABLE IF EXISTS `storage_cash`;

CREATE TABLE `storage_cash` (
  `id` int(11) unsigned NOT NULL AUTO_INCREMENT,
  `userid` int(11) NOT NULL,
  `world_id` int(11) NOT NULL,
  `bought_userid` int(11) NOT NULL,
  `sn` int(11) NOT NULL,
  `itemid` int(11) NOT NULL,
  `amount` int(3) NOT NULL DEFAULT 1,
  `from` varchar(13) NOT NULL DEFAULT '',
  `expires` bigint(20) NOT NULL DEFAULT 150842304000000000,
  PRIMARY KEY (`id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `storage_eqp` */

DROP TABLE IF EXISTS `storage_eqp`;

CREATE TABLE `storage_eqp` (
  `userid` int(11) NOT NULL,
  `world_id` int(11) NOT NULL,
  `slot` smallint(6) NOT NULL,
  `itemid` int(11) NOT NULL,
  `slots` tinyint(4) NOT NULL DEFAULT 7,
  `scrolls` tinyint(4) NOT NULL DEFAULT 0,
  `istr` smallint(6) NOT NULL DEFAULT 0,
  `idex` smallint(6) NOT NULL DEFAULT 0,
  `iint` smallint(6) NOT NULL DEFAULT 0,
  `iluk` smallint(6) NOT NULL DEFAULT 0,
  `ihp` smallint(6) NOT NULL DEFAULT 0,
  `imp` smallint(6) NOT NULL DEFAULT 0,
  `iwatk` smallint(6) NOT NULL DEFAULT 0,
  `imatk` smallint(6) NOT NULL DEFAULT 0,
  `iwdef` smallint(6) NOT NULL DEFAULT 0,
  `imdef` smallint(6) NOT NULL DEFAULT 0,
  `iacc` smallint(6) NOT NULL DEFAULT 0,
  `iavo` smallint(6) NOT NULL DEFAULT 0,
  `ihand` smallint(6) NOT NULL DEFAULT 0,
  `ispeed` smallint(6) NOT NULL DEFAULT 0,
  `ijump` smallint(6) NOT NULL DEFAULT 0,
  `cashid` bigint(20) NOT NULL,
  `expiration` bigint(20) NOT NULL DEFAULT 150842304000000000,
  KEY `userid_worldid` (`userid`,`world_id`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*Table structure for table `teleport_rock_locations` */

DROP TABLE IF EXISTS `teleport_rock_locations`;

CREATE TABLE `teleport_rock_locations` (
  `charid` int(11) NOT NULL,
  `mapindex` tinyint(3) NOT NULL,
  `mapid` int(11) NOT NULL DEFAULT 999999999,
  PRIMARY KEY (`charid`,`mapindex`),
  KEY `charid` (`charid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `user_point_transactions` */

DROP TABLE IF EXISTS `user_point_transactions`;

CREATE TABLE `user_point_transactions` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `userid` int(11) NOT NULL,
  `amount` mediumint(9) NOT NULL,
  `date` timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `note` text NOT NULL,
  `pointtype` enum('maplepoints','nx') NOT NULL,
  PRIMARY KEY (`id`),
  KEY `uid_pointtype` (`userid`,`pointtype`)
) ENGINE=MyISAM AUTO_INCREMENT=334524 DEFAULT CHARSET=latin1 COLLATE=latin1_swedish_ci;

/*Table structure for table `users` */

DROP TABLE IF EXISTS `users`;

CREATE TABLE `users` (
  `ID` int(11) NOT NULL AUTO_INCREMENT,
  `username` varchar(20) NOT NULL,
  `password` char(130) NOT NULL,
  `email` varchar(255) NOT NULL,
  `pin` int(4) unsigned DEFAULT NULL,
  `pin_secret` varchar(255) NOT NULL DEFAULT '',
  `gender` tinyint(2) DEFAULT 10,
  `admin` tinyint(1) NOT NULL DEFAULT 0,
  `char_delete_password` int(8) unsigned NOT NULL DEFAULT 11111111,
  `ban_expire` datetime NOT NULL DEFAULT '2000-01-01 00:00:00',
  `ban_reason` tinyint(2) unsigned NOT NULL DEFAULT 0,
  `ban_reason_message` text DEFAULT NULL,
  `banned_by` varchar(13) DEFAULT NULL,
  `banned_at` datetime DEFAULT NULL,
  `last_ip` varchar(45) DEFAULT NULL,
  `last_machine_id` varchar(32) DEFAULT NULL,
  `last_login` datetime NOT NULL DEFAULT '2000-01-01 00:00:00',
  `quiet_ban_expire` datetime NOT NULL DEFAULT '2000-01-01 00:00:00',
  `quiet_ban_reason` tinyint(3) NOT NULL DEFAULT 0,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `remember_token` varchar(255) NOT NULL DEFAULT '',
  `beta` tinyint(1) NOT NULL DEFAULT 0,
  `confirmed_eula` tinyint(1) NOT NULL DEFAULT 0,
  `last_unique_id` varchar(26) DEFAULT '',
  `max_unique_id_ban_count` tinyint(1) DEFAULT 5,
  `max_ip_ban_count` tinyint(1) DEFAULT 3,
  `referral_code` varchar(12) DEFAULT NULL,
  `referred_by` int(11) DEFAULT NULL,
  PRIMARY KEY (`ID`),
  KEY `username` (`username`),
  KEY `rankings` (`ban_expire`,`admin`,`ID`)
) ENGINE=MyISAM AUTO_INCREMENT=30011 DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;
