-- Create gtop_votes table

------- UP -------
CREATE TABLE `gtop_votes` (
	`ID` INT(11) NOT NULL AUTO_INCREMENT,
	`vote_date` datetime NOT NULL DEFAULT '2000-01-01 00:00:00',
	`userid` INT(11) NOT NULL,
	`handled` TINYINT(1) NOT NULL DEFAULT 0,
	`voter_ip` VARCHAR(45) DEFAULT NULL,
	PRIMARY KEY (`ID`),
	KEY `userid` (`userid`)
) ENGINE=MyISAM DEFAULT CHARSET=latin1 COLLATE=latin1_general_ci;


------- DOWN -------
