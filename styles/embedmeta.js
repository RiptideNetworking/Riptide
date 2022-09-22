window.onload = function() {
    var summary = $(".level0.summary > p").text();
    $("meta[property='og:description']").attr("content", summary);
    $("meta[property='og:url']").attr("content", window.location.href);
}