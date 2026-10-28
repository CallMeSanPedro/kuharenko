-- Проверка файла смены идентификаторов s/n, MAC и PON.
-- Одна функция. Строка результата — record курсора, без object-типа.
-- MAC и PON читаются из store_device_param.mac_address и store_device_param.pon_serial.
-- Пустой p_filial_kod отключает проверку филиала.
-- Режим и параметры приходят только в XML строки.
-- parameter_change = 1: новые SN, MAC и PON не проверяются.
-- Статус exploit_status_id = 3 («у абонента») требуется только если
-- форма владения меняется на «Собственность абонента».
-- В остальных случаях статус эксплуатации на поиск не влияет.
-- Форма владения, состояние и тех. состояние читаются из ownership_id,
-- dev_condition_id и sosttech_kod. В режиме смены идентификаторов этих элементов нет.
-- Экземпляр оборудования функция не изменяет.
--
-- v_rc := verify_identifier_change(p_store_document_id, p_xml, p_filial_kod);
--
-- <ArrayOfIdentifierChange>
--   <IdentifierChange>
--     <store_document_id>123</store_document_id>
--     <line_no>2</line_no>
--     <serial_number>OLD</serial_number>
--     <mac_address>AABBCCDDEEFF</mac_address>
--     <pon_serial>PON1</pon_serial>
--     <new_serial_number>NEW</new_serial_number>
--     <new_mac_address></new_mac_address>
--     <new_pon_serial></new_pon_serial>
--   </IdentifierChange>
-- </ArrayOfIdentifierChange>
--
-- Смена параметров, те же элементы плюс:
--   <parameter_change>1</parameter_change>
--   <ownership_id>10</ownership_id>
--   <dev_condition_id></dev_condition_id>
--   <sosttech_kod>3</sosttech_kod>

create or replace function verify_identifier_change(p_store_document_id in number,
                                                     p_xml               in clob,
                                                     p_filial_kod        in number default null) return sys_refcursor is
  -- Для уникальности нового SN и MAC экземпляр в этом статусе номер не занимает.
  c_status_not_on_store constant number := 5;
  -- «У абонента». Проверяется только при смене формы владения на «Собственность абонента».
  c_status_at_subscriber constant number := 3;

  v_xml      xmltype;
  v_ret      sys_refcursor;
  v_out      clob;
  v_comm_id  number;
  v_filial   number;
  v_card_mac varchar2(100);
  v_card_pon varchar2(100);
  v_exploit  number;
  v_cnt      number;
  v_key      varchar2(200);
  v_blocked  boolean;

  cursor v_cur(p_in_xml xmltype) is
    select nvl(case
                 when regexp_like(trim(xml_tab.store_document_id), '^[0-9]+$') then
                  to_number(trim(xml_tab.store_document_id))
               end,
               p_store_document_id) store_document_id,
           case
             when regexp_like(trim(xml_tab.line_no), '^[0-9]+$') then
              to_number(trim(xml_tab.line_no))
           end line_no,
           cast(null as number) comm_id,
           nullif(trim(xml_tab.serial_number), '') serial_number,
           nullif(trim(xml_tab.mac_address), '') mac_address,
           nullif(trim(xml_tab.pon_serial), '') pon_serial,
           nullif(trim(xml_tab.new_serial_number), '') new_serial_number,
           nullif(trim(xml_tab.new_mac_address), '') new_mac_address,
           nullif(trim(xml_tab.new_pon_serial), '') new_pon_serial,
           case when trim(xml_tab.parameter_change) = '1' then 1 else 0 end parameter_change,
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
           end sosttech_kod,
           cast(null as varchar2(100)) card_mac_address,
           cast(null as varchar2(100)) card_pon_serial,
           cast(null as number) filial_kod,
           cast(null as number) verify_status_num,
           cast(null as varchar2(1000)) verify_status_str
      from xmltable('/ArrayOfIdentifierChange/IdentifierChange' passing p_in_xml columns
                    store_document_id varchar2(50) path 'store_document_id',
                    line_no varchar2(50) path 'line_no',
                    serial_number varchar2(100) path 'serial_number',
                    mac_address varchar2(100) path 'mac_address',
                    pon_serial varchar2(100) path 'pon_serial',
                    new_serial_number varchar2(100) path 'new_serial_number',
                    new_mac_address varchar2(100) path 'new_mac_address',
                    new_pon_serial varchar2(100) path 'new_pon_serial',
                    parameter_change varchar2(10) path 'parameter_change',
                    ownership_id varchar2(50) path 'ownership_id',
                    dev_condition_id varchar2(50) path 'dev_condition_id',
                    sosttech_kod varchar2(50) path 'sosttech_kod') xml_tab;

  type t_tab is table of v_cur%rowtype index by binary_integer;
  v_tab t_tab;

  type t_count_map is table of pls_integer index by varchar2(200);
  v_current_sn t_count_map;
  v_new_sn     t_count_map;
  v_new_mac    t_count_map;

  function subscriber_ownership(p_ownership_id in number) return boolean is
    v_name varchar2(400);
  begin
    if p_ownership_id is null then
      return false;
    end if;
    begin
      -- Справочник форм владения. Колонку имени поправить, если в базе она называется иначе.
      select lower(trim(o.ownership_name))
        into v_name
        from ownership o
       where o.ownership_id = p_ownership_id;
    exception
      when no_data_found then
        return false;
      when too_many_rows then
        return false;
    end;
    return v_name = 'собственность абонента';
  end;

  function norm_mac(p_value in varchar2) return varchar2 is
  begin
    return nullif(upper(replace(replace(replace(replace(trim(p_value), ':', ''), '-', ''), '.', ''), ' ', '')), '');
  end;

  function sn_changes(p_row in v_cur%rowtype) return boolean is
  begin
    if p_row.serial_number is null or p_row.new_serial_number is null then
      return false;
    end if;
    return lower(trim(p_row.new_serial_number)) <> lower(trim(p_row.serial_number));
  end;

  function mac_changes(p_row in v_cur%rowtype) return boolean is
    v_new varchar2(100);
    v_old varchar2(100);
  begin
    v_new := norm_mac(p_row.new_mac_address);
    v_old := norm_mac(p_row.card_mac_address);
    return v_new is not null and v_new <> nvl(v_old, chr(1));
  end;

  function pon_changes(p_row in v_cur%rowtype) return boolean is
  begin
    return p_row.new_pon_serial is not null
           and lower(trim(p_row.new_pon_serial)) <> nvl(lower(trim(p_row.card_pon_serial)), chr(1));
  end;

  procedure add_error(p_index in pls_integer, p_message in varchar2) is
  begin
    v_tab(p_index).verify_status_num := -1;
    if v_tab(p_index).verify_status_str is null then
      v_tab(p_index).verify_status_str := p_message;
    elsif instr(v_tab(p_index).verify_status_str, p_message) = 0 then
      v_tab(p_index).verify_status_str := v_tab(p_index).verify_status_str || '; ' || p_message;
    end if;
  end;

  procedure inc_key(p_map in out nocopy t_count_map, p_key in varchar2) is
  begin
    if p_key is null then
      return;
    end if;
    if p_map.exists(p_key) then
      p_map(p_key) := p_map(p_key) + 1;
    else
      p_map(p_key) := 1;
    end if;
  end;

  function released_serial(p_comm_id in number) return boolean is
    v_hits pls_integer := 0;
  begin
    if p_comm_id is null then
      return false;
    end if;
    for j in 1 .. v_tab.count loop
      if v_tab(j).comm_id = p_comm_id and sn_changes(v_tab(j)) then
        v_hits := v_hits + 1;
      end if;
    end loop;
    return v_hits = 1;
  end;

  function released_mac(p_comm_id in number) return boolean is
    v_hits pls_integer := 0;
  begin
    if p_comm_id is null then
      return false;
    end if;
    for j in 1 .. v_tab.count loop
      if v_tab(j).comm_id = p_comm_id and mac_changes(v_tab(j)) then
        v_hits := v_hits + 1;
      end if;
    end loop;
    return v_hits = 1;
  end;

  procedure append_text(p_name in varchar2, p_value in varchar2) is
  begin
    if p_value is null then
      return;
    end if;
    v_out := v_out || '<' || p_name || '>' ||
             replace(replace(replace(replace(p_value, '&', '&amp;'), '<', '&lt;'), '>', '&gt;'), '"', '&quot;') ||
             '</' || p_name || '>';
  end;

  procedure append_num(p_name in varchar2, p_value in number) is
  begin
    if p_value is null then
      return;
    end if;
    append_text(p_name, to_char(p_value, 'FM99999999999999999990'));
  end;

  procedure open_empty is
  begin
    open v_ret for
      select cast(null as number) store_document_id,
             cast(null as number) line_no,
             cast(null as number) comm_id,
             cast(null as varchar2(100)) serial_number,
             cast(null as varchar2(100)) mac_address,
             cast(null as varchar2(100)) pon_serial,
             cast(null as varchar2(100)) new_serial_number,
             cast(null as varchar2(100)) new_mac_address,
             cast(null as varchar2(100)) new_pon_serial,
             cast(null as varchar2(100)) card_mac_address,
             cast(null as varchar2(100)) card_pon_serial,
             cast(null as number) filial_kod,
             cast(null as number) verify_status_num,
             cast(null as varchar2(1000)) verify_status_str
        from dual
       where 1 = 0;
  end;

begin
  if p_xml is null then
    open_empty;
    return v_ret;
  end if;

  begin
    v_xml := xmltype(p_xml);
  exception
    when others then
      raise_application_error(-20001, 'Неверный формат xml ' || sqlerrm);
  end;

  open v_cur(v_xml);
  fetch v_cur bulk collect into v_tab;
  close v_cur;

  if v_tab.count() = 0 then
    open_empty;
    return v_ret;
  end if;

  for i in 1 .. v_tab.count() loop
    if v_tab(i).serial_number is null then
      add_error(i, 'Не заполнен SN_текущий');
    else
      inc_key(v_current_sn, lower(trim(v_tab(i).serial_number)));
    end if;

    if v_tab(i).parameter_change = 0 and sn_changes(v_tab(i)) then
      inc_key(v_new_sn, lower(trim(v_tab(i).new_serial_number)));
    end if;

    if v_tab(i).serial_number is null then
      if v_tab(i).new_mac_address is not null then
        inc_key(v_new_mac, norm_mac(v_tab(i).new_mac_address));
      end if;
      continue;
    end if;

    v_comm_id := null;
    select count(distinct c.comm_id)
      into v_cnt
      from eth_comm_atu c
     where c.priznakdel = 0
       and lower(trim(c.numbdec)) = lower(trim(v_tab(i).serial_number))
       and exists (select 1
                     from store_device_param p
                    where p.comm_id = c.comm_id
                      and p.priznakdel = 0);

    if v_cnt = 0 then
      add_error(i, 'Оборудование не найдено');
    elsif v_cnt > 1 then
          add_error(i, 'Найдено больше одного экземпляра');
    else
      begin
        select c.comm_id,
               u.filial_kod,
               (select max(p.mac_address)
                  from store_device_param p
                 where p.comm_id = c.comm_id
                   and p.priznakdel = 0),
               (select max(p.pon_serial)
                  from store_device_param p
                 where p.comm_id = c.comm_id
                   and p.priznakdel = 0),
               (select max(p.exploit_status_id)
                  from store_device_param p
                 where p.comm_id = c.comm_id
                   and p.priznakdel = 0)
          into v_comm_id, v_filial, v_card_mac, v_card_pon, v_exploit
          from eth_comm_atu c
          left join store_infouzel u on u.infouzel_kod = c.uzel
         where c.priznakdel = 0
           and lower(trim(c.numbdec)) = lower(trim(v_tab(i).serial_number))
           and exists (select 1
                         from store_device_param p
                        where p.comm_id = c.comm_id
                          and p.priznakdel = 0);
      exception
        when no_data_found then
          add_error(i, 'Оборудование не найдено');
        when too_many_rows then
          v_comm_id := null;
          add_error(i, 'Найдено больше одного экземпляра');
      end;
    end if;

    if v_comm_id is null then
      if v_tab(i).new_mac_address is not null then
        inc_key(v_new_mac, norm_mac(v_tab(i).new_mac_address));
      end if;
      continue;
    end if;

    v_tab(i).comm_id := v_comm_id;
    v_tab(i).filial_kod := v_filial;
    v_tab(i).card_mac_address := v_card_mac;
    v_tab(i).card_pon_serial := v_card_pon;

    if v_tab(i).mac_address is not null
       and norm_mac(v_tab(i).mac_address) <> nvl(norm_mac(v_card_mac), chr(1)) then
      add_error(i, 'Контрольный MAC не совпал');
    end if;

    if v_tab(i).pon_serial is not null
       and lower(trim(v_tab(i).pon_serial)) <> nvl(lower(trim(v_card_pon)), chr(1)) then
      add_error(i, 'Контрольный PON не совпал');
    end if;

    if p_filial_kod is not null and v_filial is not null and v_filial <> p_filial_kod then
      add_error(i, 'Оборудование принадлежит к другому филиалу');
    end if;

    if subscriber_ownership(v_tab(i).ownership_id) and nvl(v_exploit, -1) <> c_status_at_subscriber then
      add_error(i, 'Статус эксплуатации оборудования не соответствует значению «у абонента»');
    end if;

    if v_tab(i).parameter_change = 0 and not sn_changes(v_tab(i)) and not mac_changes(v_tab(i)) and
       not pon_changes(v_tab(i)) then
      add_error(i, 'Не задано ни одного нового значения');
    end if;

    if v_tab(i).parameter_change = 0 and mac_changes(v_tab(i)) then
      inc_key(v_new_mac, norm_mac(v_tab(i).new_mac_address));
    end if;
  end loop;

  for i in 1 .. v_tab.count() loop
    v_key := lower(trim(v_tab(i).serial_number));
    if v_key is not null and v_current_sn.exists(v_key) and v_current_sn(v_key) > 1 then
      add_error(i, 'Повторяющийся SN_текущий');
    end if;

    if v_tab(i).parameter_change = 0 and sn_changes(v_tab(i)) then
      v_key := lower(trim(v_tab(i).new_serial_number));
      if v_new_sn.exists(v_key) and v_new_sn(v_key) > 1 then
        add_error(i, 'Повторяющийся SN_новый');
      end if;
    end if;

    if v_tab(i).parameter_change = 0 and v_tab(i).new_mac_address is not null then
      v_key := norm_mac(v_tab(i).new_mac_address);
      if v_key is not null and v_new_mac.exists(v_key) and v_new_mac(v_key) > 1 then
        add_error(i, 'Повторяющийся MAC_новый');
      end if;
    end if;
  end loop;

  for i in 1 .. v_tab.count() loop
    if v_tab(i).verify_status_num = -1 then
      continue;
    end if;

    if v_tab(i).parameter_change = 0 and sn_changes(v_tab(i)) then
      v_blocked := false;
      for owner in (select distinct c.comm_id
                      from eth_comm_atu c
                      join store_device_param p on p.comm_id = c.comm_id and p.priznakdel = 0
                     where c.priznakdel = 0
                       and nvl(p.exploit_status_id, -1) <> c_status_not_on_store
                       and c.comm_id <> v_tab(i).comm_id
                       and lower(trim(c.numbdec)) = lower(trim(v_tab(i).new_serial_number))) loop
        if not released_serial(owner.comm_id) then
          v_blocked := true;
          exit;
        end if;
      end loop;
      if v_blocked then
        add_error(i, 'Новый серийный номер уже есть в системе');
      end if;
    end if;

    if v_tab(i).verify_status_num = -1 then
      continue;
    end if;

    if v_tab(i).parameter_change = 0 and mac_changes(v_tab(i)) then
      v_key := norm_mac(v_tab(i).new_mac_address);
      v_blocked := false;
      for owner in (select distinct c.comm_id
                      from eth_comm_atu c
                      join store_device_param p on p.comm_id = c.comm_id and p.priznakdel = 0
                     where c.priznakdel = 0
                       and nvl(p.exploit_status_id, -1) <> c_status_not_on_store
                       and c.comm_id <> v_tab(i).comm_id
                       and upper(replace(replace(replace(replace(trim(p.mac_address), ':', ''), '-', ''), '.', ''), ' ', '')) = v_key) loop
        if not released_mac(owner.comm_id) then
          v_blocked := true;
          exit;
        end if;
      end loop;
      if v_blocked then
        add_error(i, 'Новый MAC уже есть в системе');
      end if;
    end if;

    if v_tab(i).verify_status_num = -1 then
      continue;
    end if;

    v_tab(i).verify_status_num := 1;
    v_tab(i).verify_status_str := nvl(v_tab(i).verify_status_str, 'Успешно');
  end loop;

  v_out := '<ArrayOfIdentifierChangeResult>';
  for i in 1 .. v_tab.count() loop
    v_out := v_out || '<IdentifierChangeResult>';
    append_num('store_document_id', v_tab(i).store_document_id);
    append_num('line_no', v_tab(i).line_no);
    append_num('comm_id', v_tab(i).comm_id);
    append_text('serial_number', v_tab(i).serial_number);
    append_text('mac_address', v_tab(i).mac_address);
    append_text('pon_serial', v_tab(i).pon_serial);
    append_text('new_serial_number', v_tab(i).new_serial_number);
    append_text('new_mac_address', v_tab(i).new_mac_address);
    append_text('new_pon_serial', v_tab(i).new_pon_serial);
    append_text('card_mac_address', v_tab(i).card_mac_address);
    append_text('card_pon_serial', v_tab(i).card_pon_serial);
    append_num('filial_kod', v_tab(i).filial_kod);
    append_num('verify_status_num', v_tab(i).verify_status_num);
    append_text('verify_status_str', v_tab(i).verify_status_str);
    v_out := v_out || '</IdentifierChangeResult>';
  end loop;
  v_out := v_out || '</ArrayOfIdentifierChangeResult>';

  open v_ret for
    select store_document_id,
           line_no,
           comm_id,
           serial_number,
           mac_address,
           pon_serial,
           new_serial_number,
           new_mac_address,
           new_pon_serial,
           card_mac_address,
           card_pon_serial,
           filial_kod,
           verify_status_num,
           verify_status_str
      from xmltable('/ArrayOfIdentifierChangeResult/IdentifierChangeResult' passing xmltype(v_out) columns
                    store_document_id number path 'store_document_id',
                    line_no number path 'line_no',
                    comm_id number path 'comm_id',
                    serial_number varchar2(100) path 'serial_number',
                    mac_address varchar2(100) path 'mac_address',
                    pon_serial varchar2(100) path 'pon_serial',
                    new_serial_number varchar2(100) path 'new_serial_number',
                    new_mac_address varchar2(100) path 'new_mac_address',
                    new_pon_serial varchar2(100) path 'new_pon_serial',
                    card_mac_address varchar2(100) path 'card_mac_address',
                    card_pon_serial varchar2(100) path 'card_pon_serial',
                    filial_kod number path 'filial_kod',
                    verify_status_num number path 'verify_status_num',
                    verify_status_str varchar2(1000) path 'verify_status_str');

  return v_ret;
end;
/
