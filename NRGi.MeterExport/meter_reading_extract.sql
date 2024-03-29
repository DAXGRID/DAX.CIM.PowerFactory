use PowerFactory_Maaledata
go

-- lav tabel med gis trafo navne som PF (Jackob script) vil have
drop table dbo.gis_trafo_to_pf_navn
go

SELECT * 
INTO dbo.gis_trafo_to_pf_navn
FROM OPENQUERY(shobjsql03, '

select distinct
  cast (trafo.GlobalID as varchar(50)) as trafo_globalid,

  -- PF station+spnniv+trafo navn (fx 12345_010_1)
  cast (st.id as varchar(50)) 
  + 
  ''_'' 
  + 
  (
  case 
    when st.SPAENDINGSNIVEAU in (''10 kv'') then ''004''
    when st.SPAENDINGSNIVEAU in (''15 kv'') then ''004''
	when st.SPAENDINGSNIVEAU in (''60/10 kv'') then ''010''
	when st.SPAENDINGSNIVEAU in (''60/15 kv'') then ''015''
    else ''?''
  end
  )
  +
  ''_''
  +  
  cast (trafo.LOEBENUMMER as varchar(50)) as pf_navn
from 
  data1.dataadmin.MV_TRAFO trafo
inner join 
  data1.dataadmin.MV_STATION st on st.objectid = trafo.KOMPUNDERKNUDEOBJEKTID

')
go

-- lav index på gis_trafo_to_pf_navn
CREATE INDEX idx_gis_trafo_to_pf_navn_trafo_globalid ON gis_trafo_to_pf_navn (trafo_globalid)
go

declare @fromDt as varchar(50) = '01 jan 2020';

declare @toDt as varchar(50) = '01 feb 2020';


-- Udtræk timeværdier for C-kunder
select * from
(
select 
 pn.pf_navn as Name, DATEADD(Hour, DATEDIFF(Hour, 0, timestamp),0) as Timestamp, sum(lp) / 4 as LP, sum(gp) / 4 as GP, sum(lq) / 4 as LQ,  sum(gq) / 4 as GQ
FROM 
  [PowerFactory_Maaledata].[dbo].MeterReadings
inner join
  dbo.gis_trafo_to_pf_navn pn on pn.trafo_globalid = gisid
where 
  timestamp >= @fromDt and timestamp < @toDt
group by  
  pn.pf_navn, DATEADD(Hour, DATEDIFF(Hour, 0, timestamp), 0)

union all

-- Udtræk timeværdier for A+B kunder
select 
 SdpId as Name, DATEADD(Hour, DATEDIFF(Hour, 0, timestamp),0) as Timestamp, sum(lp) / 4 as LP, sum(gp) / 4 as GP, sum(lq) / 4 as LQ,  sum(gq) / 4 as GQ
FROM 
  [PowerFactory_Maaledata].[dbo].MeterReadings
where 
  len(GisId) <> 36 and
  timestamp >= @fromDt and timestamp < @toDt
group by  
  SdpId, DATEADD(Hour, DATEDIFF(Hour, 0, timestamp), 0)
) v
order by v.Name, v.Timestamp



