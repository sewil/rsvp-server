-- Fix tinyint(1) used for non-booleans

------- UP -------
ALTER TABLE `users`   
  CHANGE `admin` `admin` TINYINT(4) DEFAULT 0  NOT NULL;


------- DOWN -------
ALTER TABLE `users`   
  CHANGE `admin` `admin` TINYINT(1) DEFAULT 0  NOT NULL;
