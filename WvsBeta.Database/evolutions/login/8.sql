------- UP -------
ALTER TABLE ipbans                             ENGINE = InnoDB;
ALTER TABLE users                              ENGINE = InnoDB;


------- DOWN -------

ALTER TABLE ipbans                             ENGINE = MyISAM;
ALTER TABLE users                              ENGINE = MyISAM;
