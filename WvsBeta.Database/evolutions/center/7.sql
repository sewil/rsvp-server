------- UP -------

CREATE TABLE `monsterbook` (
	`charid` INT NOT NULL,
	`monsterbookid` INT NOT NULL,
	`count` INT NOT NULL DEFAULT 0,
	PRIMARY KEY (`charid`, `monsterbookid`)
)
COLLATE='latin1_swedish_ci' ENGINE=MyISAM;

------- DOWN -------

DROP TABLE `monsterbook`;