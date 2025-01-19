
------- UP -------

CREATE TABLE IF NOT EXISTS `login_records` (  
  `id` INT NOT NULL AUTO_INCREMENT,
  `userid` INT,
  `uniqueid` VARCHAR(26),
  `windows_username` VARCHAR(255),
  `windows_machine_name` VARCHAR(255),
  `local_userid` VARCHAR(56),
  `first_login` DATETIME,
  `last_login` DATETIME,
  `login_count` INT,
  PRIMARY KEY (`id`) ,
  UNIQUE INDEX `unique_pc_data` (`userid` , `uniqueid` , `local_userid` , `windows_username` , `windows_machine_name`)
);


------- DOWN -------
