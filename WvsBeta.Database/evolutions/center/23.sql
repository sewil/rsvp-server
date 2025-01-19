
------- UP -------

ALTER TABLE `memos` CHANGE `message` `message` TEXT CHARSET utf8mb4 NOT NULL;

------- DOWN -------

ALTER TABLE `memos` CHANGE `message` `message` TEXT CHARSET latin1_swedish_ci NOT NULL;