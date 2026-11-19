-- Запись на карточки значений из XML, уже сохранённого на документе.
-- Вызывать после add_identifier_change_to_doc, когда store_documents.xml_file заполнен:
--   v_cnt := apply_identifier_change_xml(p_store_document_id);
-- Берутся строки ArrayOfIdentifierChangeResult с verify_status_num = 1.
-- Пустое новое значение не затирает текущее. SN и MAC пишутся в два шага,
-- чтобы обмен номерами между карточками проходил при уникальном индексе.
-- Повторный вызов не меняет карточку, у которой серийный номер уже стал новым.

create or replace function apply_identifier_change_xml(p_store_document_id in number) return number is
  v_xml clob;
  v_cnt pls_integer := 0;

  cursor v_cur(p_in_xml xmltype) is
    select xml_tab.comm_id,
           nullif(trim(xml_tab.serial_number), '') serial_number,
           nullif(trim(xml_tab.new_serial_number), '') new_serial_number,
           nullif(trim(xml_tab.new_mac_address), '') new_mac_address,
           nullif(trim(xml_tab.new_pon_serial), '') new_pon_serial,
           case
             when regexp_like(trim(xml_tab.ownership_id), '^[0-9]+$') then
              to_number(trim(xml_tab.ownership_id))
           end ownership_id,
           case
             when regexp_like(trim(xml_tab.dev_condition_id), '^[0-9]+$') then
              to_number(trim(xml_tab.dev_condition_id))
           end dev_condition_id,
           case
             when regexp_like(trim(xml_tab.sosttech_kod), '^[0-9]+$') then
              to_number(trim(xml_tab.sosttech_kod))
           end sosttech_kod
      from xmltable('/ArrayOfIdentifierChangeResult/IdentifierChangeResult' passing p_in_xml columns
                    comm_id number path 'comm_id',
                    serial_number varchar2(100) path 'serial_number',
                    new_serial_number varchar2(100) path 'new_serial_number',
                    new_mac_address varchar2(100) path 'new_mac_address',
                    new_pon_serial varchar2(100) path 'new_pon_serial',
                    ownership_id varchar2(50) path 'ownership_id',
                    dev_condition_id varchar2(50) path 'dev_condition_id',
                    sosttech_kod varchar2(50) path 'sosttech_kod',
                    verify_status_num number path 'verify_status_num') xml_tab
      join eth_comm_atu c
        on c.comm_id = xml_tab.comm_id
       and c.priznakdel = 0
       and lower(trim(c.numbdec)) = lower(trim(xml_tab.serial_number))
     where xml_tab.verify_status_num = 1
       and exists (select 1
              from store_device_param p
             where p.comm_id = c.comm_id
               and p.priznakdel = 0);

  type t_tab is table of v_cur%rowtype;
  v_tab t_tab;

  function sn_changes(p_row in v_cur%rowtype) return boolean is
  begin
    return p_row.new_serial_number is not null and
           lower(trim(p_row.new_serial_number)) <> lower(trim(p_row.serial_number));
  end;

  function temp_value(p_comm_id in number) return varchar2 is
  begin
    return '#chg#' || to_char(p_comm_id);
  end;
begin
  if p_store_document_id is null then
    raise_application_error(-20001, 'Не указан документ');
  end if;

  begin
    select d.xml_file
      into v_xml
      from store_documents d
     where d.store_document_id = p_store_document_id;
  exception
    when no_data_found then
      raise_application_error(-20004, 'Документ не найден');
  end;

  if v_xml is null then
    raise_application_error(-20005, 'На документе нет xml');
  end if;

  open v_cur(xmltype(v_xml));
  fetch v_cur bulk collect
    into v_tab;
  close v_cur;

  if v_tab.count() = 0 then
    return 0;
  end if;

  for i in 1 .. v_tab.count() loop
    if sn_changes(v_tab(i)) then
      update eth_comm_atu c
         set c.numbdec = temp_value(c.comm_id)
       where c.comm_id = v_tab(i).comm_id
         and c.priznakdel = 0;
    end if;

    if v_tab(i).new_mac_address is not null then
      update store_device_param p
         set p.mac_address = temp_value(p.comm_id)
       where p.comm_id = v_tab(i).comm_id
         and p.priznakdel = 0;
    end if;
  end loop;

  for i in 1 .. v_tab.count() loop
    if sn_changes(v_tab(i)) then
      update eth_comm_atu c
         set c.numbdec = v_tab(i).new_serial_number
       where c.comm_id = v_tab(i).comm_id
         and c.priznakdel = 0;
    end if;

    if v_tab(i).new_mac_address is not null then
      update store_device_param p
         set p.mac_address = v_tab(i).new_mac_address
       where p.comm_id = v_tab(i).comm_id
         and p.priznakdel = 0;
    end if;

    if v_tab(i).new_pon_serial is not null then
      update store_device_param p
         set p.pon_serial = v_tab(i).new_pon_serial
       where p.comm_id = v_tab(i).comm_id
         and p.priznakdel = 0;
    end if;

    if v_tab(i).ownership_id is not null then
      update store_device_param p
         set p.ownership_id = v_tab(i).ownership_id
       where p.comm_id = v_tab(i).comm_id
         and p.priznakdel = 0;
    end if;

    if v_tab(i).dev_condition_id is not null then
      update store_device_param p
         set p.dev_condition_id = v_tab(i).dev_condition_id
       where p.comm_id = v_tab(i).comm_id
         and p.priznakdel = 0;
    end if;

    if v_tab(i).sosttech_kod is not null then
      update store_device_param p
         set p.sosttech_kod = v_tab(i).sosttech_kod
       where p.comm_id = v_tab(i).comm_id
         and p.priznakdel = 0;
    end if;

    update store_doc_identifier_change d
       set d.applied_date      = sysdate,
           d.verify_status_str = 'Применено'
     where d.store_document_id = p_store_document_id
       and d.comm_id = v_tab(i).comm_id
       and d.applied_date is null;

    v_cnt := v_cnt + 1;
  end loop;

  return v_cnt;
exception
  when others then
    if v_cur%isopen then
      close v_cur;
    end if;
    raise;
end;
/
