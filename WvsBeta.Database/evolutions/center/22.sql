-- Fix memo sent_time getting reset on read

------- UP -------

ALTER TABLE `memos`   
	CHANGE `sent_time` `sent_time` TIMESTAMP DEFAULT CURRENT_TIMESTAMP() NOT NULL;


------- DOWN -------

ALTER TABLE `memos`   
	CHANGE `sent_time` `sent_time` TIMESTAMP DEFAULT CURRENT_TIMESTAMP() NOT NULL ON UPDATE current_timestamp();
