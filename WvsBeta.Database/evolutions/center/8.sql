------- UP -------

CREATE TABLE memos (
	id int not null auto_increment,
	from_name varchar(13) not null,
	to_charid int not null,
	message text not null,
	sent_time timestamp not null,
	read_time timestamp null,
	PRIMARY KEY (id)
);

------- DOWN -------

DROP TABLE memos;