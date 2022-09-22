window.onload = function() {
    var summary = $(".level0.summary > p").text();
    $("meta[property='og:description']").attr(contentAttribute, summary);
    $("meta[property='og:url']").attr(contentAttribute, window.location.href);
    
    var imageProperty = $("meta[property='og:image']");
    imageProperty.attr(contentAttribute, window.location.origin + "/" + imageProperty.attr(contentAttribute))
}