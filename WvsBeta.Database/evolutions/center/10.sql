------- UP -------

DROP TABLE IF EXISTS character_rate_credits;

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
  PRIMARY KEY (`charid`,`uid`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8


------- DOWN -------

DROP TABLE IF EXISTS character_rate_credits;
